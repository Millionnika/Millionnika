"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { apiFetch as fetch, API_URL, getErrorMessage } from "@/lib/api";

type Contract = {
  name: string;
  costNerve: number;
  successChance: number;
  rewardCredits: number;
  rewardExperience: number;
};

type ActionResult = {
  succeeded: boolean;
  message: string;
  player: { credits: number; nerve: number; experience: number };
};

type ActionLog = {
  actionType: string;
  succeeded: boolean;
  message: string;
  createdAtUtc: string;
};

export default function ContractsPage() {
  const router = useRouter();
  const [contract, setContract] = useState<Contract | null>(null);
  const [logs, setLogs] = useState<ActionLog[]>([]);
  const [result, setResult] = useState<ActionResult | null>(null);
  const [error, setError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function loadData() {
    const [contractResponse, logsResponse] = await Promise.all([
      fetch(`${API_URL}/api/game/contract`, { credentials: "include" }),
      fetch(`${API_URL}/api/game/logs`, { credentials: "include" }),
    ]);

    if (contractResponse.status === 401 || logsResponse.status === 401) {
      router.replace("/login");
      return;
    }

    if (!contractResponse.ok || !logsResponse.ok) {
      throw new Error("Game data request failed");
    }

    setContract((await contractResponse.json()) as Contract);
    setLogs((await logsResponse.json()) as ActionLog[]);
  }

  useEffect(() => {
    void loadData().catch(() => setError("Не удалось загрузить контракт."));
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  async function executeContract() {
    setError("");
    setIsSubmitting(true);
    const requestId = crypto.randomUUID();

    try {
      const response = await fetch(`${API_URL}/api/game/contract/execute`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ requestId }),
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
      await loadData();
    } catch {
      setError("Сервер игры недоступен.");
    } finally {
      setIsSubmitting(false);
    }
  }

  if (!contract && !error) return <main><p className="loading">Загрузка контракта…</p></main>;

  return (
    <main>
      <section className="game-panel">
        <Link className="back-link" href="/profile">← Профиль</Link>
        <p className="eyebrow game-eyebrow">Контракты</p>
        <h1 className="game-title">{contract?.name ?? "Контракт недоступен"}</h1>

        {contract && (
          <div className="mission-details">
            <span>Стоимость <strong>{contract.costNerve} хладнокровия</strong></span>
            <span>Шанс <strong>{Math.round(contract.successChance * 100)}%</strong></span>
            <span>Награда <strong>{contract.rewardCredits} кредитов · {contract.rewardExperience} опыта</strong></span>
          </div>
        )}

        {result && <p className={result.succeeded ? "action-result success" : "action-result failure"}>{result.message}</p>}
        {error && <p className="form-error">{error}</p>}

        <button className="primary-button action-button" type="button" onClick={executeContract} disabled={!contract || isSubmitting}>
          {isSubmitting ? "Выполняется…" : "Выполнить контракт"}
        </button>

        <h2 className="section-title">Последние действия</h2>
        <div className="action-log">
          {logs.length === 0 && <p className="field-hint">Журнал пока пуст.</p>}
          {logs.map((log) => (
            <article key={`${log.createdAtUtc}-${log.message}`}>
              <span>{new Date(log.createdAtUtc).toLocaleString("ru-RU")}</span>
              <p>{log.message}</p>
            </article>
          ))}
        </div>
      </section>
    </main>
  );
}
