"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { GameNav } from "@/components/GameNav";
import { BattleScreen, type BattleParticipant, type BattleWeapon } from "@/components/BattleScreen";
import { apiFetch, API_URL, getErrorMessage, publishPlayerState } from "@/lib/api";
import { getPendingAction } from "@/lib/pendingAction";

type Target = { id: string; name: string; description: string; minimumHuntingLevel: number; energyCost: number; health: number; rewardCredits: number; rewardExperience: number; rewardHuntingExperience: number; unlocked: boolean; lockedReason: string | null };
type Location = { id: string; name: string; description: string; minimumHuntingLevel: number; unlocked: boolean; targets: Target[] };
type BattleEquipment = { slot: string; type: "weapon" | "armor"; name: string; damage: number; accuracy: number; armor: number; coverage: Record<string, number> };
type BattleProtection = { source: "natural"; armor: number; coverage: Record<string, number> };
type Fight = { fightId: string; replayed: boolean; status: string; turn: number; maximumTurns: number; playerHealth: number; maximumPlayerHealth: number; robotHealth: number; maximumRobotHealth: number; expiresAtUtc: string; nextActionAtUtc: string | null; message: string; log: string[]; weapons: BattleWeapon[]; participants: BattleParticipant[]; playerEquipment: BattleEquipment[]; robotEquipment: BattleEquipment[]; robotProtection: BattleProtection[]; creditsAwarded: number; experienceAwarded: number; huntingExperienceAwarded: number; huntingLevel: number; energy: number; levelIncreased: boolean; errorCode: string | null; requiredAmmoWeapon: string | null; requiredAmmoName: string | null };

export default function HuntingPage() {
  const router = useRouter();
  const [locations, setLocations] = useState<Location[]>([]);
  const [fight, setFight] = useState<Fight | null>(null);
  const [message, setMessage] = useState(""); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [missingAmmo, setMissingAmmo] = useState<{ weapon: string; ammunition: string } | null>(null);

  const loadLocations = useCallback(async () => {
    const response = await apiFetch(`${API_URL}/api/game/hunting/locations`, { credentials: "include" });
    if (response.status === 401) { router.replace("/login"); return; }
    if (!response.ok) throw new Error("locations");
    setLocations(await response.json() as Location[]);
  }, [router]);

  const load = useCallback(async () => {
    const [locationsResponse, fightResponse] = await Promise.all([
      apiFetch(`${API_URL}/api/game/hunting/locations`, { credentials: "include" }),
      apiFetch(`${API_URL}/api/game/hunting/fight`, { credentials: "include" }),
    ]);
    if (locationsResponse.status === 401 || fightResponse.status === 401) { router.replace("/login"); return; }
    if (!locationsResponse.ok || (!fightResponse.ok && fightResponse.status !== 204)) throw new Error("load");
    setLocations(await locationsResponse.json() as Location[]);
    const activeFight = fightResponse.status === 204 ? null : await fightResponse.json() as Fight;
    setFight(activeFight);
    if (activeFight) publishFightHealth(activeFight);
  }, [router]);

  const refreshExpiredFight = useCallback(() => {
    void load().catch(() => setError("Не удалось получить итог боя. Обновите страницу."));
  }, [load]);

  useEffect(() => { void load().catch(() => setError("Не удалось загрузить охоту.")).finally(() => setLoading(false)); }, [load]);

  async function start(targetId: string) {
    const pending = getPendingAction("hunt_start", { targetId });
    await mutate("/api/game/hunting/start", { requestId: pending.requestId, targetId }, pending.receivedResponse);
  }
  async function turn(action: "attack" | "reload", slot: string) {
    const pending = getPendingAction("hunt_turn", { action, slot });
    await mutate("/api/game/hunting/turn", { requestId: pending.requestId, action, slot }, pending.receivedResponse);
  }
  async function mutate(path: string, body: object, received: () => void) {
    setBusy(true); setError(""); setMissingAmmo(null);
    try {
      const response = await apiFetch(`${API_URL}${path}`, { method: "POST", headers: { "Content-Type": "application/json" }, credentials: "include", body: JSON.stringify(body) });
      received();
      if (response.status === 401) { router.replace("/login"); return; }
      if (!response.ok && response.status !== 409 && response.status !== 429) { setError(await getErrorMessage(response)); return; }
      const result = await response.json() as Fight; setMessage(result.message); publishFightHealth(result);
      if (result.errorCode === "compatible_ammo_missing" && result.requiredAmmoWeapon && result.requiredAmmoName)
        setMissingAmmo({ weapon: result.requiredAmmoWeapon, ammunition: result.requiredAmmoName });
      if (result.fightId !== "00000000-0000-0000-0000-000000000000") setFight(result);
      if (result.status !== "active") await loadLocations();
    } catch { setError("Ответ потерян. Повторите ту же кнопку: requestId сохранён безопасно."); }
    finally { setBusy(false); }
  }

  return <main><section className="game-panel">
    <GameNav /><Link className="back-link" href="/activities">← Активности</Link>
    <p className="eyebrow game-eyebrow">Навык «Охота на роботов»</p><h1 className="game-title">Охота</h1>
    {message && <p className="action-result success">{message}</p>}
    {missingAmmo && <p className="form-error">Оружие: {missingAmmo.weapon}. Нужны: {missingAmmo.ammunition}. <Link href="/shop">Перейти в магазин</Link></p>}
    {error && <p className="form-error">{error}</p>}
    {fight && fight.status !== "active" && <p className="action-result battle-result">Последний бой: {fight.message} Здоровье {fight.playerHealth}/{fight.maximumPlayerHealth}. Награда: {fight.creditsAwarded} кредитов, {fight.experienceAwarded} общего опыта, {fight.huntingExperienceAwarded} опыта охоты.</p>}
    {loading && <div className="battle-loading" role="status"><span />Загружаем данные боя…</div>}
    {fight && <BattleScreen
      status={fight.status} turn={fight.turn} maximumTurns={fight.maximumTurns}
      expiresAtUtc={fight.expiresAtUtc} nextActionAtUtc={fight.nextActionAtUtc}
      playerHealth={fight.playerHealth} maximumPlayerHealth={fight.maximumPlayerHealth}
      opponentHealth={fight.robotHealth} maximumOpponentHealth={fight.maximumRobotHealth}
      participants={fight.participants ?? []} weapons={fight.weapons} log={fight.log} busy={busy}
      onAction={turn} onExpired={refreshExpiredFight}
    />}
    {!loading && fight?.status !== "active" && <div className="hunting-locations">
      {locations.map((location) => <section className={`hunting-location${location.unlocked ? "" : " locked"}`} key={location.id}>
        <h2>{location.name}</h2><p>{location.description}</p>
        {!location.unlocked && <p className="contract-lock">Откроется на уровне охоты {location.minimumHuntingLevel}</p>}
        <div className="contract-grid">{location.targets.map((target) => <article className={`contract-card${target.unlocked ? "" : " locked"}`} key={target.id}>
          <div><h2>{target.name}</h2><p>{target.description}</p><p>Здоровье {target.health} · Энергия {target.energyCost}</p><p>Награда: {target.rewardCredits} кредитов · {target.rewardExperience} общего опыта · {target.rewardHuntingExperience} опыта охоты</p>{target.lockedReason && <p className="contract-lock">{target.lockedReason}</p>}</div>
          <button className="primary-button" disabled={!target.unlocked || busy} onClick={() => start(target.id)}>Начать охоту</button>
        </article>)}</div>
      </section>)}
    </div>}
  </section></main>;
}

function publishFightHealth(fight: Fight) {
  publishPlayerState({ fightActive: fight.status === "active", healthCurrent: fight.playerHealth, healthMaximum: fight.maximumPlayerHealth });
}
