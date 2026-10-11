"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { API_URL } from "@/lib/api";
import { GameNav } from "@/components/GameNav";

type Resource = {
  current: number;
  maximum: number;
  nextRecoveryAtUtc: string | null;
};

type Profile = {
  username: string;
  description: string;
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
  huntingProgress: Progress;
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
            <p className="profile-description">{profile.description || "Описание пока не заполнено."}</p>
          </div>
        </header>

        <h2 className="section-title">Прогресс</h2>
        <section className="progress-grid" aria-label="Прогресс персонажа">
          <ProgressCard title="Общий уровень" progress={profile.generalProgress} />
          <ProgressCard title="Взлом" progress={profile.hackingProgress} />
          <ProgressCard title="Охота на роботов" progress={profile.huntingProgress} />
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

function Stat({ title, value }: Readonly<{ title: string; value: number }>) {
  return <article className="stat-card"><span>{title}</span><strong>{value}</strong></article>;
}

function formatCoverage(value: string) {
  const labels: Record<string, string> = { head: "голова", throat: "горло", heart: "сердце", chest: "грудь", stomach: "живот", arms: "руки", hands: "кисти", groin: "пах", legs: "ноги", feet: "ступни" };
  try { return Object.entries(JSON.parse(value) as Record<string, number>).map(([part, percent]) => `${labels[part] ?? part} ${percent}%`).join(", "); }
  catch { return "покрытие не указано"; }
}
