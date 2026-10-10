type DatabaseStatus = {
  available: boolean;
  message: string;
};

type HealthStatus = {
  serverAvailable: boolean;
  database: DatabaseStatus;
};

async function getHealth(): Promise<HealthStatus> {
  const backendUrl = process.env.BACKEND_URL ?? "http://localhost:5080";

  try {
    const response = await fetch(`${backendUrl}/api/health`, {
      cache: "no-store",
      signal: AbortSignal.timeout(3000),
    });

    if (!response.ok) {
      throw new Error("Backend returned an error");
    }

    return (await response.json()) as HealthStatus;
  } catch {
    return {
      serverAvailable: false,
      database: {
        available: false,
        message: "Проверка невозможна: сервер игры недоступен",
      },
    };
  }
}

function StatusCard({ title, available, message }: Readonly<{
  title: string;
  available: boolean;
  message: string;
}>) {
  return (
    <article className="status-card">
      <div className={`status-dot ${available ? "status-dot--online" : "status-dot--offline"}`} />
      <div>
        <h2>{title}</h2>
        <p className={available ? "online" : "offline"}>
          {available ? "Доступен" : "Недоступен"}
        </p>
        <span>{message}</span>
      </div>
    </article>
  );
}

export const dynamic = "force-dynamic";

export default async function Home() {
  const health = await getHealth();

  return (
    <main>
      <section className="hero">
        <p className="eyebrow">Киберпанк-мегаполис ждёт</p>
        <h1>Миллионика</h1>
        <p className="lead">Основа проекта запущена. Игровые механики появятся на следующих этапах.</p>

        <div className="hero-actions">
          <Link className="primary-button" href="/register">Создать персонажа</Link>
          <Link className="secondary-button" href="/login">Войти</Link>
          <Link className="text-link" href="/profile">Мой профиль</Link>
        </div>

        <div className="status-grid">
          <StatusCard
            title="Сервер игры"
            available={health.serverAvailable}
            message={health.serverAvailable ? "ASP.NET Core отвечает" : "Запустите backend"}
          />
          <StatusCard
            title="База данных"
            available={health.database.available}
            message={health.database.message}
          />
        </div>
      </section>
    </main>
  );
}
import Link from "next/link";

