import asyncio
import math
import random
import struct
import logging
from dataclasses import dataclass, field
from pymodbus.server import StartAsyncTcpServer
from pymodbus.datastore import ModbusSequentialDataBlock, ModbusSlaveContext, ModbusServerContext

logging.basicConfig()
log = logging.getLogger()
log.setLevel(logging.INFO)

# --- Function code → datastore table (pymodbus convention) ---
#   1 = Coils (CO, RW)   2 = Discrete Inputs (DI, RO)
#   3 = Holding (HR, RW) 4 = Input Registers (IR, RO)
FC_CO, FC_DI, FC_HR, FC_IR = 1, 2, 3, 4


def float_to_regs(value: float) -> list[int]:
    """IEEE 754 float → [high_word, low_word], big-endian word order (Modbus ABCD)."""
    b = struct.pack(">f", value)
    return [(b[0] << 8) | b[1], (b[2] << 8) | b[3]]


def regs_to_float(regs: list[int]) -> float:
    """[high_word, low_word] (ABCD) → IEEE 754 float. Безопасно к недозаполненным регистрам."""
    if not regs or len(regs) < 2:
        return 0.0
    return struct.unpack(">f", struct.pack(">HH", regs[0] & 0xFFFF, regs[1] & 0xFFFF))[0]


def clamp(v: float, lo: float, hi: float) -> float:
    return max(lo, min(hi, v))


# --- Внутреннее физическое состояние процессов (то, что в реальности живёт в железе) ---
@dataclass
class PlantState:
    # Slave 2 — асинхронный двигатель с ЧРП
    motor_rpm: float = 0.0
    motor_temp: float = 25.0
    motor_power: float = 0.0
    # Slave 3 — насосная станция (резервуар)
    tank_press: float = 0.0   # raw 0-65535
    tank_level: float = 50.0  # %


def setup_context() -> ModbusServerContext:
    """
    Три устройства на одной TCP-шине. Роли регистров жёстко разделены:
      - Датчики/измерения  → IR (FC04) / DI (FC02): пишет ТОЛЬКО прошивка, оператор read-only.
      - Уставки/команды     → HR (FC03) / CO (FC01): пишет ТОЛЬКО оператор, прошивка их читает и держит.
    """
    slaves = {
        # Slave 1: ADAM-6017 (Advantech) — модуль аналогового ВВОДА. Выходов нет.
        #   IR 0-3: AI0..AI3 (16-bit 0-65535)   DI 0: авария
        0x01: ModbusSlaveContext(
            ir=ModbusSequentialDataBlock(0, [0] * 10),
            di=ModbusSequentialDataBlock(0, [0] * 10),
        ),
        # Slave 2: Siemens S7-1200 — привод двигателя.
        #   Измерения (RO):  IR 0-1 факт. RPM (f32), IR 2-3 темп. (f32), IR 4-5 мощность (f32)
        #                    DI 0 "вращается", DI 1 "перегрев"
        #   Управление (RW): HR 0-1 уставка RPM (f32), CO 0 пуск/стоп
        0x02: ModbusSlaveContext(
            ir=ModbusSequentialDataBlock(0, [0] * 20),
            di=ModbusSequentialDataBlock(0, [0] * 10),
            hr=ModbusSequentialDataBlock(0, [0] * 20),
            co=ModbusSequentialDataBlock(0, [0] * 10),
        ),
        # Slave 3: Schneider M221 — насосная станция / резервуар.
        #   Измерения (RO):  IR 0 давление (16-bit), IR 1 уровень (16-bit)
        #                    DI 0 "давление достигнуто", DI 1 "низкий уровень"
        #   Управление (RW): HR 0 уставка давления (16-bit), CO 0 реле насоса
        0x03: ModbusSlaveContext(
            ir=ModbusSequentialDataBlock(0, [0] * 10),
            di=ModbusSequentialDataBlock(0, [0] * 10),
            hr=ModbusSequentialDataBlock(0, [0] * 10),
            co=ModbusSequentialDataBlock(0, [0] * 10),
        ),
    }
    ctx = ModbusServerContext(slaves=slaves, single=False)

    # Заводские дефолты уставок (как у реального прибора при первом включении).
    ctx[0x02].setValues(FC_HR, 0, float_to_regs(1200.0))  # уставка RPM по умолчанию
    ctx[0x02].setValues(FC_CO, 0, [0])                     # двигатель остановлен
    ctx[0x03].setValues(FC_HR, 0, [40000])                 # уставка давления (~6 Bar в сырых ед.)
    ctx[0x03].setValues(FC_CO, 0, [0])                     # насос выключен
    return ctx


def update_adam(ctx: ModbusServerContext, t: float) -> None:
    """Slave 1 — независимые датчики. Модуль ввода: оператор сюда не пишет в принципе."""
    s1 = ctx[0x01]
    s1.setValues(FC_IR, 0, [
        int((math.sin(t * 0.3) + 1) * 0.5 * 65535),        # temp   -50..150°C
        int((math.sin(t * 0.5 + 1.0) + 1) * 0.5 * 65535),  # press   0..10 Bar
        int((math.cos(t * 0.2) + 1) * 0.5 * 65535),        # humid   0..100%
        int(13107 + math.sin(t * 0.4) * 13107),            # 4-20mA
    ])
    s1.setValues(FC_DI, 0, [1 if random.random() > 0.97 else 0])  # авария


def update_motor(ctx: ModbusServerContext, st: PlantState) -> None:
    """
    Slave 2 — ЗАМКНУТАЯ ПЕТЛЯ. Прошивка ЧИТАЕТ команды оператора (HR/CO) и крутит процесс.
    Оператор пишет уставку RPM и пуск → факт. обороты подтягиваются, темп/мощность следуют.
    """
    run = bool(ctx[0x02].getValues(FC_CO, 0, 1)[0])
    setpoint = regs_to_float(ctx[0x02].getValues(FC_HR, 0, 2))

    target = setpoint if run else 0.0
    st.motor_rpm += (target - st.motor_rpm) * 0.25                 # инерция разгона/выбега
    load = st.motor_rpm / 1450.0
    st.motor_temp += ((25.0 + load * 65.0) - st.motor_temp) * 0.08  # нагрев под нагрузкой
    st.motor_power = st.motor_rpm * 0.011 * (1 + random.uniform(-0.02, 0.02)) if run else 0.0

    ctx[0x02].setValues(FC_IR, 0, float_to_regs(st.motor_rpm))
    ctx[0x02].setValues(FC_IR, 2, float_to_regs(st.motor_temp))
    ctx[0x02].setValues(FC_IR, 4, float_to_regs(st.motor_power))
    ctx[0x02].setValues(FC_DI, 0, [1 if st.motor_rpm > 50 else 0])   # вращается
    ctx[0x02].setValues(FC_DI, 1, [1 if st.motor_temp > 85 else 0])  # перегрев


def update_pump(ctx: ModbusServerContext, st: PlantState) -> None:
    """
    Slave 3 — ЗАМКНУТАЯ ПЕТЛЯ. Реле насоса (CO) и уставка давления (HR) задаются оператором.
    Насос вкл → давление растёт к уставке, резервуар наполняется. Выкл → стравливается.
    """
    pump = bool(ctx[0x03].getValues(FC_CO, 0, 1)[0])
    sp_press = ctx[0x03].getValues(FC_HR, 0, 1)[0]

    if pump:
        st.tank_press += (sp_press - st.tank_press) * 0.15
        st.tank_level += (95.0 - st.tank_level) * 0.04
    else:
        st.tank_press += (0.0 - st.tank_press) * 0.12
        st.tank_level += (10.0 - st.tank_level) * 0.03

    st.tank_press = clamp(st.tank_press, 0, 65535)
    st.tank_level = clamp(st.tank_level, 0, 100)

    ctx[0x03].setValues(FC_IR, 0, [int(st.tank_press)])
    ctx[0x03].setValues(FC_IR, 1, [int(st.tank_level / 100.0 * 65535)])
    ctx[0x03].setValues(FC_DI, 0, [1 if sp_press > 0 and st.tank_press >= sp_press * 0.95 else 0])
    ctx[0x03].setValues(FC_DI, 1, [1 if st.tank_level < 20 else 0])


async def update_values_loop(ctx: ModbusServerContext) -> None:
    st = PlantState()
    t = 0.0
    tick = 0
    log.info("Process simulator started (sensors generated, setpoints operator-driven).")

    while True:
        try:
            update_adam(ctx, t)
            update_motor(ctx, st)
            update_pump(ctx, st)

            if tick % 10 == 0:
                log.info(
                    "Motor: rpm=%.0f temp=%.1f pwr=%.1f | Pump: press=%.0f level=%.0f%%",
                    st.motor_rpm, st.motor_temp, st.motor_power, st.tank_press, st.tank_level,
                )

            t += 0.5
            tick += 1
            await asyncio.sleep(1.0)

        except Exception as e:
            log.error(f"Generator error: {e}")
            await asyncio.sleep(2)


async def run_server() -> None:
    context = setup_context()
    asyncio.create_task(update_values_loop(context))
    log.info("Modbus TCP simulator listening on 0.0.0.0:5020")
    await StartAsyncTcpServer(context=context, address=("0.0.0.0", 5020))


if __name__ == "__main__":
    try:
        asyncio.run(run_server())
    except KeyboardInterrupt:
        log.info("Simulator stopped.")
