# Переменные для команд
DC = sudo docker-compose
DOCKER = sudo docker
# Список контейнеров для ручного удаления (Nuclear Option)
CONTAINERS = modbus_client modbus_web_gatewey adam_db modbus_sim

.PHONY: help up down restart status logs logs-client logs-sim logs-gateway clean db-shell shell-client

# Помощь (выводится по умолчанию)
help:
	@echo "🛠️  Управление проектом DipMod"
	@echo "----------------------------------------------------------------"
	@echo "Команды:"
	@echo "  make up          -> Собрать и запустить все контейнеры (в фоне)"
	@echo "  make down        -> Остановить контейнеры (без удаления данных)"
	@echo "  make restart     -> Перезапустить всё (down + up)"
	@echo "  make status      -> Показать список запущенных контейнеров"
	@echo ""
	@echo "Логи:"
	@echo "  make logs        -> Логи всех сервисов (tail 100)"
	@echo "  make logs-client -> Логи Modbus клиента (подробно)"
	@echo "  make logs-sim    -> Логи Симулятора"
	@echo "  make logs-web    -> Логи Web Gateway"
	@echo ""
	@echo "Отладка и Обслуживание:"
	@echo "  make clean       -> ☢️  ЯДЕРНАЯ ЧИСТКА: Принудительно удалить контейнеры"
	@echo "                      (Используйте, если docker-compose выдает ошибки)"
	@echo "  make db-shell    -> Зайти в SQL консоль базы данных"
	@echo "  make shell-client-> Зайти в bash контейнера клиента"

# Основные команды
up:
	$(DC) up -d --build

down:
	$(DC) down

restart: down up

status:
	$(DOCKER) ps

# Логирование
logs:
	$(DC) logs -f --tail=100

logs-client:
	$(DOCKER) logs -f --tail=100 modbus_client

logs-sim:
	$(DOCKER) logs -f --tail=100 modbus_sim

logs-web:
	$(DOCKER) logs -f --tail=100 modbus_web_gatewey

# Исправление ошибок (то самое решение проблемы KeyError: 'ContainerConfig')
clean:
	@echo "🧹 Принудительная остановка и удаление контейнеров..."
	-$(DOCKER) stop $(CONTAINERS)
	-$(DOCKER) rm $(CONTAINERS)
	@echo "✅ Готово. Теперь можно запускать 'make up'"

# Утилиты
db-shell:
	$(DOCKER) exec -it adam_db psql -U admin -d AdamMonitoring

shell-client:
	$(DOCKER) exec -it modbus_client /bin/bash
