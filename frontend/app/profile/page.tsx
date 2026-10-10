"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { apiFetch as fetch, API_URL, getErrorMessage } from "@/lib/api";
import { GameNav } from "@/components/GameNav";

type Resource = {
  current: number;
  maximum: number;
  nextRecoveryAtUtc: string | null;
};

type Profile = {
  username: string;
  role: string;
  level: number;
  credits: number;
  energy: Resource;
  nerve: Resource;
  health: Resource;
  strength: number;
  speed: number;
  defense: number;
  dexterity: number;
  experience: number;
  generalProgress: Progress;
  hackingProgress: Progress;
  equipment: EquippedItem[];
};

type Progress = { level: number; experience: number; currentLevelThreshold: number; nextLevelThreshold: number | null; isMaximum: boolean };
type EquippedItem = { instanceId: string; slot: string; name: string; type: string; damage: number; accuracy: number; armor: number; armorCoverage: string };

const equipmentSlots = [
  ["primary", "Основное оружие"], ["secondary", "Вторичное оружие"], ["melee", "Ближний бой"], ["temporary", "Временное"],
  ["helmet", "Голова"], ["body", "Корпус"], ["pants", "Ноги"], ["gloves", "Перчатки"], ["boots", "Обувь"],
] as const;

export default function ProfilePage() {
  const router = useRouter();
  const [profile, setProfile] = useState<Profile | null>(null);
  const [error, setError] = useState("");

  const loadProfile = useCallback(async (signal?: AbortSignal) => {
    try {
      const response = await fetch(`${API_URL}/api/profile`, {
        credentials: "include",
        signal,
      });

      if (response.status === 401) {
        router.replace("/login");
        return;
      }

      if (!response.ok) {
        throw new Error("Profile request failed");
      }

      setProfile((await response.json()) as Profile);
    } catch (requestError) {
      if (!(requestError instanceof DOMException && requestError.name === "AbortError")) {
        setError("Не удалось загрузить профиль.");
      }
    }
  }, [router]);

  useEffect(() => {
    const controller = new AbortController();

    void loadProfile(controller.signal);
    return () => controller.abort();
  }, [loadProfile]);

  async function logout() {
    try {
      const response = await fetch(`${API_URL}/api/auth/logout`, { method: "POST", credentials: "include" });
      if (!response.ok && response.status !== 401) {
        setError(await getErrorMessage(response));
        return;
      }
      router.replace("/login");
      router.refresh();
    } catch { setError("Не удалось завершить сессию. Повторите выход после восстановления связи."); }
  }

  if (error) {
    return <main><section className="auth-panel"><p className="form-error">{error}</p></section></main>;
  }

  if (!profile) {
    return <main><p className="loading">Загрузка профиля…</p></main>;
  }

  return (
    <main>
      <section className="profile-panel">
        <GameNav />
        <header className="profile-header">
          <div>
            <p className="eyebrow">Профиль игрока</p>
            <h1 className="profile-title">{profile.username}</h1>
            <p className="level">Уровень {profile.level} · Опыт {profile.experience}</p>
          </div>
          <button className="secondary-button" type="button" onClick={logout}>Выйти</button>
        </header>

        <section className="resource-grid" aria-label="Ресурсы">
          <ResourceCard title="Кредиты" value={profile.credits.toLocaleString("ru-RU")} />
          <ResourceCard
            title="Энергия"
            value={`${profile.energy.current} / ${profile.energy.maximum}`}
            nextRecoveryAtUtc={profile.energy.nextRecoveryAtUtc}
            onRecoveryElapsed={loadProfile}
          />
          <ResourceCard
            title="Хладнокровие"
            value={`${profile.nerve.current} / ${profile.nerve.maximum}`}
            nextRecoveryAtUtc={profile.nerve.nextRecoveryAtUtc}
            onRecoveryElapsed={loadProfile}
          />
          <ResourceCard
            title="Здоровье"
            value={`${profile.health.current} / ${profile.health.maximum}`}
            recoveryDisabled
          />
        </section>

        <h2 className="section-title">Прогресс</h2>
        <section className="progress-grid" aria-label="Прогресс персонажа">
          <ProgressCard title="Общий уровень" progress={profile.generalProgress} />
          <ProgressCard title="Взлом" progress={profile.hackingProgress} />
        </section>

        <h2 className="section-title">Характеристики</h2>
        <section className="stats-grid" aria-label="Характеристики">
          <Stat title="Сила" value={profile.strength} />
          <Stat title="Скорость" value={profile.speed} />
          <Stat title="Защита" value={profile.defense} />
          <Stat title="Ловкость" value={profile.dexterity} />
        </section>

        <h2 className="section-title">Экипировка</h2>
        <section className="stats-grid" aria-label="Экипировка">
          {equipmentSlots.map(([slot, title]) => {
            const item = profile.equipment.find((entry) => entry.slot === slot);
            return <article className="stat-card" key={slot}><span>{title}</span><strong>{item?.name ?? "Пусто"}</strong>{item && <small>{item.type === "weapon" ? `Урон ${item.damage} · Точность ${item.accuracy}` : `Броня ${item.armor} · ${formatCoverage(item.armorCoverage)}`}</small>}</article>;
          })}
        </section>

        <nav className="game-navigation" aria-label="Игровые действия">
          <Link className="primary-button" href="/contracts">Контракты</Link>
          <Link className="secondary-button" href="/hacking">Взлом</Link>
          <Link className="secondary-button" href="/implant-center">Имплант-центр</Link>
          <Link className="secondary-button" href="/shop">Магазин</Link>
          <Link className="secondary-button" href="/inventory">Инвентарь</Link>
          <Link className="secondary-button" href="/players">Игроки</Link>
          <Link className="secondary-button" href="/friends">Друзья</Link>
          {profile.role === "Admin" && <Link className="primary-button" href="/admin">Админка</Link>}
        </nav>

        <Link className="back-link profile-home-link" href="/">На главную</Link>
      </section>
    </main>
  );
}

function ProgressCard({ title, progress }: Readonly<{ title: string; progress: Progress }>) {
  if (progress.isMaximum) return <article className="progress-card"><strong>{title}: {progress.level}</strong><span>Максимальный уровень · всего опыта {progress.experience}</span></article>;
  const gained = progress.experience - progress.currentLevelThreshold;
  const required = progress.nextLevelThreshold! - progress.currentLevelThreshold;
  return <article className="progress-card"><strong>{title}: {progress.level}</strong><span>{gained} / {required} до следующего уровня</span><progress value={gained} max={required} /></article>;
}

function ResourceCard({
  title,
  value,
  nextRecoveryAtUtc,
  recoveryDisabled = false,
  onRecoveryElapsed,
}: Readonly<{
  title: string;
  value: string | number;
  nextRecoveryAtUtc?: string | null;
  recoveryDisabled?: boolean;
  onRecoveryElapsed?: () => void;
}>) {
  return (
    <article className="resource-card">
      <span>{title}</span>
      <strong>{value}</strong>
      {recoveryDisabled && <small>Автовосстановления нет</small>}
      {nextRecoveryAtUtc === null && <small>Полностью восстановлено</small>}
      {typeof nextRecoveryAtUtc === "string" && (
        <RecoveryCountdown targetUtc={nextRecoveryAtUtc} onElapsed={onRecoveryElapsed} />
      )}
    </article>
  );
}

function Stat({ title, value }: Readonly<{ title: string; value: number }>) {
  return <article className="stat-card"><span>{title}</span><strong>{value}</strong></article>;
}

function RecoveryCountdown({
  targetUtc,
  onElapsed,
}: Readonly<{ targetUtc: string; onElapsed?: () => void }>) {
  const [remainingSeconds, setRemainingSeconds] = useState(() => getRemainingSeconds(targetUtc));

  useEffect(() => {
    setRemainingSeconds(getRemainingSeconds(targetUtc));
    const timer = window.setInterval(() => {
      const remaining = getRemainingSeconds(targetUtc);
      setRemainingSeconds(remaining);
      if (remaining === 0) {
        window.clearInterval(timer);
        onElapsed?.();
      }
    }, 1000);

    return () => window.clearInterval(timer);
  }, [targetUtc, onElapsed]);

  const minutes = Math.floor(remainingSeconds / 60);
  const seconds = remainingSeconds % 60;
  return <small>До восстановления: {minutes}:{seconds.toString().padStart(2, "0")}</small>;
}

function getRemainingSeconds(targetUtc: string) {
  return Math.max(0, Math.ceil((new Date(targetUtc).getTime() - Date.now()) / 1000));
}

function formatCoverage(value: string) {
  const labels: Record<string, string> = { head: "голова", throat: "горло", heart: "сердце", chest: "грудь", stomach: "живот", arms: "руки", hands: "кисти", groin: "пах", legs: "ноги", feet: "ступни" };
  try { return Object.entries(JSON.parse(value) as Record<string, number>).map(([part, percent]) => `${labels[part] ?? part} ${percent}%`).join(", "); }
  catch { return "покрытие не указано"; }
}
