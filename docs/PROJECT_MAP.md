# Короткая карта проекта

Вход для нового разработчика: [AGENTS.md](../AGENTS.md) → этот файл → только папки и тесты нужной задачи. Фактическое состояние проверяй по коду. [ARCHITECTURE.md](ARCHITECTURE.md) содержит подробные связи, [COMBAT_SPEC.md](COMBAT_SPEC.md) — бой, [STATUS.md](STATUS.md) — текущую сводку и историю. [README.md](../README.md) содержит запуск и команды проверок.

| Задача | С чего начать | Где проверять |
| --- | --- | --- |
| Экран или админка | `frontend/app/<маршрут>/page.tsx`, `frontend/components`, `frontend/app/globals.css`, `frontend/lib/api.ts` | `npm run build --prefix frontend`; страница на ПК и 390 px |
| НПЦ, создание, добыча, зонная защита | `backend/Millionnika.Api/Admin/NpcAdminService.cs`, `NpcContracts.cs`, `frontend/app/admin/page.tsx`, `backend/Millionnika.Api/Hunting/NpcZoneArmor.cs` | `backend/Millionnika.Api.Tests/Admin/NpcCatalogIntegrationTests.cs`; тесты охоты |
| Назначение НПЦ в локации | `backend/Millionnika.Api/Admin/HuntingAssignmentAdminService.cs`, `Entities/HuntingLocationTarget.cs`, `Hunting/HuntingService.cs` | тесты Admin и Hunting; каталог `/hunting` |
| Бой, шанс попадания, награды | `backend/Millionnika.Api/Hunting/HuntingService.cs`, `CombatCalculator.cs`, `Configuration/HuntingOptions.cs`, `frontend/components/BattleScreen.tsx` | `backend/Millionnika.Api.Tests/Hunting`; [COMBAT_SPEC.md](COMBAT_SPEC.md) |
| Предметы, магазин, инвентарь | `backend/Millionnika.Api/Inventory`, `Entities/Item.cs`, `frontend/app/inventory`, `frontend/app/shop` | `backend/Millionnika.Api.Tests/Inventory` |
| Контракты, Взлом, тренировка | `backend/Millionnika.Api/Game`, `frontend/app/contracts`, `frontend/app/hacking`, `frontend/app/implant-center` | `backend/Millionnika.Api.Tests/Game` |
| Аккаунты, доступ, аудит | `backend/Millionnika.Api/Auth`, `Admin`, `Security`, `frontend/proxy.ts` | тесты Auth и Admin |
| База и миграции | `backend/Millionnika.Api/Data/GameDbContext.cs`, `Entities`, `Migrations` | инструкция по тестовой базе и миграциям в [README.md](../README.md) |

Запрос идёт из страницы Next.js через `frontend/lib/api.ts` к endpoint ASP.NET Core, затем к сервису и PostgreSQL. Для нового поля обычно нужны DTO, серверная проверка, модель/миграция при изменении схемы, форма и целевой тест. Сначала найди существующий маршрут через `rg`, затем проследи только его цепочку.

Сейчас каталог НПЦ и охотничьи роботы связаны через назначение в локации. У НПЦ единая добыча во всех локациях; при первом назначении добыча шаблона переносится в запись охоты. Новый бой сохраняет снимок параметров, поэтому правки НПЦ не меняют уже начатый бой. Несколько НПЦ в локации означают выбор отдельной цели; группового боя пока нет.
