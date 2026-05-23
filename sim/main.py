import asyncio
import math
import random
import struct
import logging
from pymodbus.server import StartAsyncTcpServer
from pymodbus.datastore import ModbusSequentialDataBlock, ModbusSlaveContext, ModbusServerContext

logging.basicConfig()
log = logging.getLogger()
log.setLevel(logging.INFO)


def float_to_regs(value: float) -> list[int]:
    """IEEE 754 float → [high_word, low_word], big-endian word order (Modbus standard)."""
    b = struct.pack(">f", value)
    return [(b[0] << 8) | b[1], (b[2] << 8) | b[3]]


def setup_context() -> ModbusServerContext:
    slaves = {
        # Slave 1: ADAM-6017 (Advantech)
        #   Input Registers (FC04) addr 0-3: AI0..AI3, 16-bit 0-65535
        #   Discrete Inputs  (FC02) addr 0:  DI0, alarm
        0x01: ModbusSlaveContext(
            ir=ModbusSequentialDataBlock(0, [0] * 10),
            di=ModbusSequentialDataBlock(0, [0] * 10),
        ),
        # Slave 2: Siemens S7-1200
        #   Holding Registers (FC03) 32-bit float, high-word first:
        #     addr 100-101: Motor RPM
        #     addr 102-103: Temperature setpoint
        #     addr 104-105: Power kW
        0x02: ModbusSlaveContext(
            hr=ModbusSequentialDataBlock(0, [0] * 120),
        ),
        # Slave 3: Schneider Modicon M221
        #   Holding Registers (FC03) addr 0: flow rate (16-bit)
        #   Coils             (FC01) addr 0: pump relay output
        #   Discrete Inputs   (FC02) addr 0: start button input
        0x03: ModbusSlaveContext(
            hr=ModbusSequentialDataBlock(0, [0] * 10),
            co=ModbusSequentialDataBlock(0, [0] * 10),
            di=ModbusSequentialDataBlock(0, [0] * 10),
        ),
    }
    return ModbusServerContext(slaves=slaves, single=False)


async def update_values_loop(context: ModbusServerContext) -> None:
    t = 0.0
    log.info("Value generator started.")

    while True:
        try:
            # --- Slave 1: ADAM-6017 ---
            # AI0: Temperature   → -50..150°C  (scaler in DB: in 0-65535, out -50..150)
            # AI1: Pressure in   → 0..10 Bar
            # AI2: Humidity      → 0..100%
            # AI3: Current 4-20mA → DB in 0-65535, out 4..20
            # DI0: Alarm (редкий импульс)
            s1 = context[0x01]
            s1.setValues(4, 0, [
                int((math.sin(t * 0.3) + 1) * 0.5 * 65535),           # temp
                int((math.sin(t * 0.5 + 1.0) + 1) * 0.5 * 65535),     # pressure
                int((math.cos(t * 0.2) + 1) * 0.5 * 65535),           # humidity
                int(13107 + math.sin(t * 0.4) * 13107),                # 4-20mA
            ])
            s1.setValues(2, 0, [1 if random.random() > 0.97 else 0])   # alarm

            # --- Slave 2: Siemens S7-1200 (32-bit float) ---
            s2 = context[0x02]
            s2.setValues(3, 100, float_to_regs(1000.0 + math.sin(t * 0.2) * 450.0))   # RPM 550-1450
            s2.setValues(3, 102, float_to_regs(65.0 + math.sin(t * 0.1) * 15.0))      # °C  50-80
            s2.setValues(3, 104, float_to_regs(15.0 + math.cos(t * 0.3) * 8.0))       # kW  7-23

            # --- Slave 3: Schneider M221 ---
            s3 = context[0x03]
            s3.setValues(3, 0, [int(20000 + math.sin(t * 0.6) * 15000)])   # flow 0-65535
            s3.setValues(1, 0, [1 if (int(t) % 12 < 9) else 0])            # pump relay (75% on)
            s3.setValues(2, 0, [1 if random.random() > 0.95 else 0])        # start button

            t += 0.5
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
