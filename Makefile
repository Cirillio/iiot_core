# Переменные для команд
DC = sudo docker-compose
DOCKER = sudo docker
# Список контейнеров для ручного удаления (Nuclear Option)
CONTAINERS = modbus_client modbus_web_gatewey adam_db modbus_sim

.PHONY: help up up-db up-client up-sim up-gateway up-cloudflared down restart status logs logs-client logs-sim logs-gateway clean db-shell shell-client

# Помощь (выводится по умолчанию)
help:
	@echo "🛠️  Управление проектом DipMod"
	@echo "----------------------------------------------------------------"
	@echo "Команды:"
	@echo "  make up          -> Собрать и запустить все контейнеры (в фоне)"
	@echo "  make up-db       -> Запустить только базу данных"
	@echo "  make up-client   -> Запустить только Modbus клиент"
	@echo "  make up-sim      -> Запустить только симулятор"
	@echo "  make up-gateway  -> Запустить только Web Gateway"
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

up-db:
	$(DC) up -d db

up-client:
	$(DC) up -d client

up-sim:
	$(DC) up -d sim

up-gateway:
	$(DC) up -d gateway

up-cloudflared:
	$(DC) up -d cloudflared

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
	@echo "🧹 Принудительная остановка и удаление контейнеров и томов..."
	-$(DC) down -v
	-$(DOCKER) stop $(CONTAINERS)
	-$(DOCKER) rm $(CONTAINERS)
	@echo "✅ Готово. Теперь можно запускать 'make up'"

# Сброс базы данных (удаление данных)
reset-db:
	@echo "🧨 Сброс базы данных (удаление всех данных)..."
	$(DC) down -v
	@echo "✅ База сброшена. Запустите 'make up-db' для инициализации новым конфигом."


# Утилиты
db-shell:
	$(DOCKER) exec -it adam_db psql -U admin -d AdamMonitoring


shell-client:
	$(DOCKER) exec -it modbus_client /bin/bash
