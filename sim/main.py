import asyncio
import math
import logging
from datetime import datetime
from pymodbus.server import StartAsyncTcpServer
from pymodbus.datastore import ModbusSequentialDataBlock, ModbusDeviceContext, ModbusServerContext

logging.basicConfig()
log = logging.getLogger()
log.setLevel(logging.INFO)

def setup_context():
    store = ModbusDeviceContext(
        di=ModbusSequentialDataBlock(0, [0]*100), 
        co=ModbusSequentialDataBlock(0, [0]*100), 
        hr=ModbusSequentialDataBlock(0, [0]*100), 
        ir=ModbusSequentialDataBlock(0, [0]*100)  
    )
    return ModbusServerContext(devices=store, single=True)

async def update_values_loop(context):
    counter = 0.0
    slave_id = 0x01

    print(f"[{datetime.now().strftime('%H:%M:%S')}] Симулятор запущен (3 канала).")

    while True:
        try:
            # 1. ANALOG 1 (Port 7) - Temperature
            val_7 = int((math.sin(counter) + 1) * 32767)
            
            # 2. ANALOG 2 (Port 6) - Pressure
            val_6 = int((math.cos(counter * 0.5) + 1) * 32767)

            context[slave_id].setValues(4, 7, [val_7])
            context[slave_id].setValues(4, 6, [val_6])

            # 3. DIGITAL (Port 0) - Pump Status (blinks every cycle)
            digital_val = 1 if (int(counter * 2) % 2 == 0) else 0
            context[slave_id].setValues(2, 0, [digital_val])

            if int(counter * 5) % 5 == 0:
                print(f"{datetime.now().strftime('%H:%M:%S')} | AI(7)={val_7} | AI(6)={val_6} | DI(0)={digital_val}")

            counter += 0.1
            await asyncio.sleep(0.5)

        except Exception as e:
            print(f"Ошибка: {e}")
            await asyncio.sleep(1)

async def run_server():
    context = setup_context()
    asyncio.create_task(update_values_loop(context))
    await StartAsyncTcpServer(context=context, address=("0.0.0.0", 5020))

if __name__ == "__main__":
    asyncio.run(run_server())
