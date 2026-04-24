FROM python:3.12-slim

WORKDIR /app

# Фиксируем версию, чтобы не ломать код при обновлениях
RUN pip install --no-cache-dir pymodbus==3.5.4

COPY main.py .

CMD ["python", "-u", "main.py"]
