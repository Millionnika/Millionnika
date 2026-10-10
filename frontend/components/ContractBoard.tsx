"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { GameNav } from "@/components/GameNav";
import { apiFetch as fetch, API_URL, getErrorMessage } from "@/lib/api";
import { getPendingAction } from "@/lib/pendingAction";

type Contract = { id: string; name: string; description: string; costNerve: number; successChance: number; rewardCredits: number; rewardExperience: number; rewardHackingExperience: number; minimumLevel: number; minimumHackingLevel: number; isAvailable: boolean; lockReason: string | null };
type ActionResult = { succeeded: boolean; message: string; player: { credits: number; nerve: number; experience: number; level: number; hackingExperience: number; hackingLevel: number } };
type ActionLog = { actionType: string; succeeded: boolean; message: string; createdAtUtc: string };
type Progress = { level: number; experience: number; currentLevelThreshold: number; nextLevelThreshold: number | null; isMaximum: boolean };
type Profile = { hackingProgress: Progress };

export function ContractBoard({ direction, eyebrow, title, showHackingProgress = false }: Readonly<{ direction: "normal" | "hacking"; eyebrow: string; title: string; showHackingProgress?: boolean }>) {
  const router = useRouter();
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [logs, setLogs] = useState<ActionLog[]>([]);
  const [progress, setProgress] = useState<Progress | null>(null);
  const [result, setResult] = useState<ActionResult | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState("");

  async function loadData() {
    const requests = [fetch(`${API_URL}/api/game/contracts?direction=${direction}`, { credentials: "include" }), fetch(`${API_URL}/api/game/logs`, { credentials: "include" })];
    if (showHackingProgress) requests.push(fetch(`${API_URL}/api/profile`, { credentials: "include" }));
    const responses = await Promise.all(requests);
    if (responses.some((response) => response.status === 401)) { router.replace("/login"); return; }
    if (responses.some((response) => !response.ok)) throw new Error("Game data request failed");
    setContracts((await responses[0].json()) as Contract[]);
    setLogs((await responses[1].json()) as ActionLog[]);
    if (showHackingProgress) setProgress(((await responses[2].json()) as Profile).hackingProgress);
    setLoading(false);
  }

  useEffect(() => { void loadData().catch(() => { setError("Не удалось загрузить задания."); setLoading(false); }); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  async function executeContract(contract: Contract) {
    if (!contract.isAvailable) return;
    setError(""); setBusyId(contract.id);
    try {
      const pending = getPendingAction("contract", { contractId: contract.id });
      const response = await fetch(`${API_URL}/api/game/contract/execute`, { method: "POST", headers: { "Content-Type": "application/json" }, credentials: "include", body: JSON.stringify({ requestId: pending.requestId, contractId: contract.id }) });
      pending.receivedResponse();
      if (response.status === 401) { router.replace("/login"); return; }
      if (!response.ok && response.status !== 409) { setError(await getErrorMessage(response)); return; }
      setResult((await response.json()) as ActionResult);
      await loadData();
    } catch (exception) { setError(exception instanceof Error ? exception.message : "Сервер игры недоступен."); }
    finally { setBusyId(""); }
  }

  if (loading) return <main><p className="loading">Загрузка заданий…</p></main>;
  return <main><section className="game-panel contract-panel">
    <GameNav />
    <p className="eyebrow game-eyebrow">{eyebrow}</p><h1 className="game-title">{title}</h1>
    {progress && <ProgressCard title="Взлом" progress={progress} />}
    {result && <p className={result.succeeded ? "action-result success" : "action-result failure"}>{result.message} Сейчас хладнокровие: {result.player.nerve}.</p>}
    {error && <p className="form-error">{error}</p>}
    {contracts.length === 0 && <p className="field-hint">Активных заданий сейчас нет.</p>}
    <div className="contract-grid">{contracts.map((contract) => <article className={`contract-card${contract.isAvailable ? "" : " locked"}`} key={contract.id}>
      <div><h2>{contract.name}</h2><p>{contract.description}</p></div>
      <div className="mission-details"><span>Стоимость <strong>{contract.costNerve} хладнокровия</strong></span><span>Шанс <strong>{Math.round(contract.successChance * 100)}%</strong></span><span>Награда <strong>{contract.rewardCredits} кредитов · {contract.rewardExperience} общего опыта{contract.rewardHackingExperience > 0 ? ` · ${contract.rewardHackingExperience} опыта Взлома` : ""}</strong></span></div>
      {!contract.isAvailable && <p className="contract-lock">{contract.lockReason}</p>}
      <button className="primary-button" type="button" disabled={busyId !== "" || !contract.isAvailable} onClick={() => executeContract(contract)}>{busyId === contract.id ? "Выполняется…" : contract.isAvailable ? "Выполнить" : "Закрыто"}</button>
    </article>)}</div>
    <h2 className="section-title">Последние действия</h2><div className="action-log">{logs.length === 0 && <p className="field-hint">Журнал пока пуст.</p>}{logs.map((log) => <article key={`${log.createdAtUtc}-${log.message}`}><span>{new Date(log.createdAtUtc).toLocaleString("ru-RU")}</span><p>{log.message}</p></article>)}</div>
  </section></main>;
}

function ProgressCard({ title, progress }: Readonly<{ title: string; progress: Progress }>) {
  if (progress.isMaximum) return <section className="progress-card"><strong>{title}: уровень {progress.level}</strong><span>Максимальный уровень · опыт продолжает накапливаться ({progress.experience})</span></section>;
  const gained = progress.experience - progress.currentLevelThreshold;
  const required = progress.nextLevelThreshold! - progress.currentLevelThreshold;
  return <section className="progress-card"><strong>{title}: уровень {progress.level}</strong><span>{gained} / {required} до следующего уровня</span><progress value={gained} max={required} /></section>;
}
