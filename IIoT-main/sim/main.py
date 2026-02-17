import asyncio
import math
from datetime import datetime
from pymodbus.server import StartAsyncTcpServer
from pymodbus.datastore import ModbusSequentialDataBlock, ModbusDeviceContext, ModbusServerContext

# Настройка хранилища
# di = Discrete Inputs (цифровые входы)
# hr = Holding Registers (аналоговые входы)
store = ModbusDeviceContext(
    di=ModbusSequentialDataBlock(0, [0]*10),
    
    hr=ModbusSequentialDataBlock(0, [0]*10)
)
context = ModbusServerContext(devices=store, single=True)

async def update_values(context):
    """Имитация датчика на канале 7"""
    counter = 0
    while True:
        now = datetime.now()
        await asyncio.sleep(1)
        # RAW значение от 0 до 65535
        val = int((math.sin(counter) + 1) * 32767)
        
        # Запись в Holding Registers (3) на адрес 7
        slave_id = 0x00 
        context[slave_id].setValues(3, 7, [val])
        
        # Переключение цифрового порта 0
        digital_val = 1 if (int(counter) % 2 == 0) else 0
        context[slave_id].setValues(2, 0, [digital_val])
        
        print(f"{now.strftime('%H:%M:%S')} | Update: Analog(7)={val} | Digital(0)={digital_val}")
        counter += 0.2

async def main():
    print("Симулятор ADAM-6017 запущен на порту 5020...")
    asyncio.create_task(update_values(context))
    await StartAsyncTcpServer(context=context, address=("0.0.0.0", 5020))

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\nОстановка.")