"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { API_URL, getErrorMessage } from "@/lib/api";

const stats = [
  { value: "strength", label: "Сила" },
  { value: "speed", label: "Скорость" },
  { value: "defense", label: "Защита" },
  { value: "dexterity", label: "Ловкость" },
];

type TrainingConfig = { energyCost: number; statGain: number; experienceGain: number };
type ActionResult = { succeeded: boolean; message: string; player: { energy: number; experience: number } };

export default function ImplantCenterPage() {
  const router = useRouter();
  const [config, setConfig] = useState<TrainingConfig | null>(null);
  const [selectedStat, setSelectedStat] = useState("strength");
  const [result, setResult] = useState<ActionResult | null>(null);
  const [error, setError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    fetch(`${API_URL}/api/game/training`, { credentials: "include" })
      .then((response) => {
        if (response.status === 401) {
          router.replace("/login");
          return null;
        }
        if (!response.ok) throw new Error("Training config request failed");
        return response.json() as Promise<TrainingConfig>;
      })
      .then((data) => data && setConfig(data))
      .catch(() => setError("Не удалось загрузить параметры тренировки."));
  }, [router]);

  async function train() {
    setError("");
    setIsSubmitting(true);

    try {
      const response = await fetch(`${API_URL}/api/game/training/execute`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ requestId: crypto.randomUUID(), stat: selectedStat }),
      });

      if (response.status === 401) {
        router.replace("/login");
        return;
      }

      if (!response.ok && response.status !== 409) {
        setError(await getErrorMessage(response));
        return;
      }

      setResult((await response.json()) as ActionResult);
    } catch {
      setError("Сервер игры недоступен.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main>
      <section className="game-panel">
        <Link className="back-link" href="/profile">← Профиль</Link>
        <p className="eyebrow game-eyebrow">Имплант-центр</p>
        <h1 className="game-title">Тренировка</h1>
        {config && <p className="lead compact-lead">Стоимость: {config.energyCost} энергии. Результат: +{config.statGain} к характеристике и +{config.experienceGain} опыта.</p>}

        <div className="stat-selector" role="radiogroup" aria-label="Характеристика">
          {stats.map((stat) => (
            <label key={stat.value} className={selectedStat === stat.value ? "selected" : ""}>
              <input type="radio" name="stat" value={stat.value} checked={selectedStat === stat.value} onChange={() => setSelectedStat(stat.value)} />
              {stat.label}
            </label>
          ))}
        </div>

        {result && <p className={result.succeeded ? "action-result success" : "action-result failure"}>{result.message}</p>}
        {error && <p className="form-error">{error}</p>}

        <button className="primary-button action-button" type="button" onClick={train} disabled={!config || isSubmitting}>
          {isSubmitting ? "Тренировка…" : "Тренироваться"}
        </button>
      </section>
    </main>
  );
}
