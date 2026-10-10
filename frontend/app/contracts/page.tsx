"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { GameNav } from "@/components/GameNav";
import { apiFetch as fetch, API_URL, getErrorMessage } from "@/lib/api";
import { getPendingAction } from "@/lib/pendingAction";

type Contract = { id: string; name: string; description: string; costNerve: number; successChance: number; rewardCredits: number; rewardExperience: number };
type ActionResult = { succeeded: boolean; message: string; player: { credits: number; nerve: number; experience: number } };
type ActionLog = { actionType: string; succeeded: boolean; message: string; createdAtUtc: string };

export default function ContractsPage() {
  const router = useRouter();
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [logs, setLogs] = useState<ActionLog[]>([]);
  const [result, setResult] = useState<ActionResult | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState("");

  async function loadData() {
    const [contractsResponse, logsResponse] = await Promise.all([
      fetch(`${API_URL}/api/game/contracts`, { credentials: "include" }),
      fetch(`${API_URL}/api/game/logs`, { credentials: "include" }),
    ]);
    if (contractsResponse.status === 401 || logsResponse.status === 401) { router.replace("/login"); return; }
    if (!contractsResponse.ok || !logsResponse.ok) throw new Error("Game data request failed");
    setContracts((await contractsResponse.json()) as Contract[]);
    setLogs((await logsResponse.json()) as ActionLog[]);
    setLoading(false);
  }

  useEffect(() => { void loadData().catch(() => { setError("Не удалось загрузить каталог контрактов."); setLoading(false); }); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  async function executeContract(contract: Contract) {
    setError(""); setBusyId(contract.id);
    try {
      const pending = getPendingAction("contract", { contractId: contract.id });
      const response = await fetch(`${API_URL}/api/game/contract/execute`, {
        method: "POST", headers: { "Content-Type": "application/json" }, credentials: "include",
        body: JSON.stringify({ requestId: pending.requestId, contractId: contract.id }),
      });
      pending.receivedResponse();
      if (response.status === 401) { router.replace("/login"); return; }
      if (!response.ok && response.status !== 409) { setError(await getErrorMessage(response)); return; }
      setResult((await response.json()) as ActionResult);
      await loadData();
    } catch (exception) { setError(exception instanceof Error ? exception.message : "Сервер игры недоступен."); }
    finally { setBusyId(""); }
  }

  if (loading) return <main><p className="loading">Загрузка контрактов…</p></main>;
  return <main><section className="game-panel contract-panel">
    <GameNav />
    <p className="eyebrow game-eyebrow">Контракты</p>
    <h1 className="game-title">Доска сомнительных возможностей</h1>
    {result && <p className={result.succeeded ? "action-result success" : "action-result failure"}>{result.message} Сейчас хладнокровие: {result.player.nerve}.</p>}
    {error && <p className="form-error">{error}</p>}
    {contracts.length === 0 && <p className="field-hint">Активных контрактов сейчас нет.</p>}
    <div className="contract-grid">{contracts.map((contract) => <article className="contract-card" key={contract.id}>
      <div><h2>{contract.name}</h2><p>{contract.description}</p></div>
      <div className="mission-details"><span>Стоимость <strong>{contract.costNerve} хладнокровия</strong></span><span>Шанс <strong>{Math.round(contract.successChance * 100)}%</strong></span><span>Награда <strong>{contract.rewardCredits} кредитов · {contract.rewardExperience} опыта</strong></span></div>
      <button className="primary-button" type="button" disabled={busyId !== ""} onClick={() => executeContract(contract)}>{busyId === contract.id ? "Выполняется…" : "Выполнить"}</button>
    </article>)}</div>
    <h2 className="section-title">Последние действия</h2>
    <div className="action-log">{logs.length === 0 && <p className="field-hint">Журнал пока пуст.</p>}{logs.map((log) => <article key={`${log.createdAtUtc}-${log.message}`}><span>{new Date(log.createdAtUtc).toLocaleString("ru-RU")}</span><p>{log.message}</p></article>)}</div>
  </section></main>;
}
