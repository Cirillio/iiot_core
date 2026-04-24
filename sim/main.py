import asyncio
import math
import random
import logging
from datetime import datetime
from pymodbus.server import StartAsyncTcpServer
from pymodbus.datastore import ModbusSequentialDataBlock, ModbusSlaveContext, ModbusServerContext

# Настройка логирования
logging.basicConfig()
log = logging.getLogger()
log.setLevel(logging.INFO)

def setup_context():
    """
    Настройка контекста сервера.
    Инициализируем 100 регистров для каждого типа данных, чтобы избежать выхода за границы.
    """
    slaves = {
        0x01: ModbusSlaveContext(
            di=ModbusSequentialDataBlock(0, [0]*100), # Discrete Inputs (0-99)
            ir=ModbusSequentialDataBlock(0, [0]*100)  # Input Registers (0-99)
        ),
        0x02: ModbusSlaveContext(
            di=ModbusSequentialDataBlock(0, [0]*100), 
            ir=ModbusSequentialDataBlock(0, [0]*100)
        ),
        0x03: ModbusSlaveContext(
            di=ModbusSequentialDataBlock(0, [0]*100), 
            ir=ModbusSequentialDataBlock(0, [0]*100)
        )
    }
    return ModbusServerContext(slaves=slaves, single=False)

async def update_values_loop(context):
    """
    Цикл генерации данных, синхронизированный с iiot_init.sql.
    """
    counter = 0.0
    print(f"[{datetime.now().strftime('%H:%M:%S')}] Цикл генерации данных запущен.")

    while True:
        try:
            slave1 = context[0x01]
            slave2 = context[0x02]
            slave3 = context[0x03]

            # --- SLAVE 1: Pump Station Alpha ---
            # Port 1 (Analog): In Pressure
            p_in = int((math.sin(counter) + 1) * 32767)
            # Port 2 (Analog): Out Pressure
            p_out = int((math.sin(counter + 1.5) + 1) * 32767 * 0.8) + 13000
            # Port 3 (Analog): Pump Temp
            p_temp = int((math.cos(counter * 0.3) + 1) * 25000) + 5000
            # Port 0 (Digital): Pump Status
            p_stat = 1 if (int(counter) % 10 < 7) else 0 # 70% времени работает

            slave1.setValues(4, 1, [p_in])
            slave1.setValues(4, 2, [p_out])
            slave1.setValues(4, 3, [p_temp])
            slave1.setValues(2, 0, [p_stat])

            # --- SLAVE 2: Chiller Unit A ---
            # Port 3 (Analog): Refrigerant Temp
            c_temp = int((math.sin(counter * 0.5) + 1) * 15000)
            # Port 1 (Analog): Pressure High
            c_press = int(45000 + random.randint(-2000, 2000))
            # Port 0 (Digital): Compressor Status
            c_stat = 1 if p_stat == 1 else 0 # Синхронно с насосом

            slave2.setValues(4, 3, [c_temp])
            slave2.setValues(4, 1, [c_press])
            slave2.setValues(2, 0, [c_stat])

            # --- SLAVE 3: HVAC Main ---
            # Port 3 (Analog): CO2 Level
            h_co2 = int((counter % 20) / 20 * 65535)
            # Port 1 (Analog): Supply Temp
            h_temp = int(28000 + math.sin(counter * 0.2) * 4000)
            # Port 0 (Digital): Filter Error
            h_filt = 1 if (random.random() > 0.98) else 0 # Редкая ошибка

            slave3.setValues(4, 3, [h_co2])
            slave3.setValues(4, 1, [h_temp])
            slave3.setValues(2, 0, [h_filt])

            if int(counter * 10) % 100 == 0:
                log.info(f"Update: PUMP_STAT:{p_stat} | CHILL_P:{c_press} | HVAC_CO2:{h_co2}")

            counter += 0.5
            await asyncio.sleep(1.0)

        except Exception as e:
            log.error(f"Ошибка эмуляции: {e}")
            await asyncio.sleep(2)

async def run_server():
    """
    Запуск асинхронного Modbus TCP сервера.
    """
    context = setup_context()
    asyncio.create_task(update_values_loop(context))
    
    print(f"[{datetime.now().strftime('%H:%M:%S')}] Слушаю Modbus на 0.0.0.0:5020...")
    
    await StartAsyncTcpServer(
        context=context, 
        address=("0.0.0.0", 5020)
    )

if __name__ == "__main__":
    try:
        print("Запуск симулятора IIoT (v3.5 Sync)...")
        asyncio.run(run_server())
    except KeyboardInterrupt:
        print("Симулятор остановлен.")
    except Exception as e:
        print(f"Критическая ошибка: {e}")
        import traceback
        traceback.print_exc()
