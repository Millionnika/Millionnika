"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { GameNav } from "@/components/GameNav";
import { apiFetch, API_URL, getErrorMessage } from "@/lib/api";
import { getPendingAction } from "@/lib/pendingAction";

type Target = { id: string; name: string; description: string; minimumHuntingLevel: number; energyCost: number; health: number; rewardCredits: number; rewardExperience: number; rewardHuntingExperience: number; unlocked: boolean; lockedReason: string | null };
type Location = { id: string; name: string; description: string; minimumHuntingLevel: number; unlocked: boolean; targets: Target[] };
type Weapon = { slot: string; name: string; ammoInClip: number; reserveAmmo: number; clipSize: number };
type Fight = { fightId: string; replayed: boolean; status: string; turn: number; maximumTurns: number; playerHealth: number; maximumPlayerHealth: number; robotHealth: number; maximumRobotHealth: number; expiresAtUtc: string; nextActionAtUtc: string | null; message: string; log: string[]; weapons: Weapon[]; creditsAwarded: number; experienceAwarded: number; huntingExperienceAwarded: number; huntingLevel: number; energy: number; levelIncreased: boolean };

export default function HuntingPage() {
  const router = useRouter();
  const [locations, setLocations] = useState<Location[]>([]);
  const [fight, setFight] = useState<Fight | null>(null);
  const [message, setMessage] = useState(""); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    const [locationsResponse, fightResponse] = await Promise.all([
      apiFetch(`${API_URL}/api/game/hunting/locations`, { credentials: "include" }),
      apiFetch(`${API_URL}/api/game/hunting/fight`, { credentials: "include" }),
    ]);
    if (locationsResponse.status === 401 || fightResponse.status === 401) { router.replace("/login"); return; }
    if (!locationsResponse.ok || (!fightResponse.ok && fightResponse.status !== 204)) throw new Error("load");
    setLocations(await locationsResponse.json() as Location[]);
    setFight(fightResponse.status === 204 ? null : await fightResponse.json() as Fight);
  }, [router]);

  useEffect(() => { void load().catch(() => setError("Не удалось загрузить охоту.")); }, [load]);

  async function start(targetId: string) {
    const pending = getPendingAction("hunt_start", { targetId });
    await mutate("/api/game/hunting/start", { requestId: pending.requestId, targetId }, pending.receivedResponse);
  }
  async function turn(action: "attack" | "reload", slot: string) {
    const pending = getPendingAction("hunt_turn", { action, slot });
    await mutate("/api/game/hunting/turn", { requestId: pending.requestId, action, slot }, pending.receivedResponse);
  }
  async function mutate(path: string, body: object, received: () => void) {
    setBusy(true); setError("");
    try {
      const response = await apiFetch(`${API_URL}${path}`, { method: "POST", headers: { "Content-Type": "application/json" }, credentials: "include", body: JSON.stringify(body) });
      received();
      if (response.status === 401) { router.replace("/login"); return; }
      if (!response.ok && response.status !== 409 && response.status !== 429) { setError(await getErrorMessage(response)); return; }
      const result = await response.json() as Fight; setMessage(result.message);
      if (result.fightId !== "00000000-0000-0000-0000-000000000000") setFight(result);
      if (result.status !== "active") await load();
    } catch { setError("Ответ потерян. Повторите ту же кнопку: requestId сохранён безопасно."); }
    finally { setBusy(false); }
  }

  return <main><section className="game-panel">
    <GameNav /><Link className="back-link" href="/activities">← Активности</Link>
    <p className="eyebrow game-eyebrow">Навык «Охота на роботов»</p><h1 className="game-title">Охота</h1>
    {message && <p className="action-result success">{message}</p>}{error && <p className="form-error">{error}</p>}
    {fight && fight.status !== "active" && <p className="action-result success">Последний бой: {fight.message} Здоровье {fight.playerHealth}/{fight.maximumPlayerHealth}. Награда: {fight.creditsAwarded} кредитов, {fight.experienceAwarded} общего опыта, {fight.huntingExperienceAwarded} опыта охоты.</p>}
    {fight?.status === "active" ? <FightPanel fight={fight} busy={busy} onTurn={turn} /> : <div className="hunting-locations">
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

function FightPanel({ fight, busy, onTurn }: Readonly<{ fight: Fight; busy: boolean; onTurn: (action: "attack" | "reload", slot: string) => void }>) {
  return <section className="fight-panel">
    <div className="fight-health"><strong>Игрок: {fight.playerHealth}/{fight.maximumPlayerHealth}</strong><strong>Робот: {fight.robotHealth}/{fight.maximumRobotHealth}</strong><span>Ход {fight.turn}/{fight.maximumTurns}</span></div>
    <div className="fight-weapons">{fight.weapons.map((weapon) => <article className="stat-card" key={weapon.slot}><span>{weapon.slot}</span><strong>{weapon.name}</strong><small>{weapon.clipSize ? `Магазин ${weapon.ammoInClip}/${weapon.clipSize} · запас ${weapon.reserveAmmo}` : "Боеприпасы не нужны"}</small><div className="friend-buttons"><button className="primary-button" disabled={busy || (weapon.clipSize > 0 && weapon.ammoInClip === 0)} onClick={() => onTurn("attack", weapon.slot)}>Атака</button>{weapon.clipSize > 0 && <button className="secondary-button" disabled={busy || weapon.reserveAmmo === 0 || weapon.ammoInClip >= weapon.clipSize} onClick={() => onTurn("reload", weapon.slot)}>Перезарядить</button>}</div></article>)}</div>
    <div className="action-log"><h2>Журнал</h2>{fight.log.slice().reverse().map((line, index) => <article key={`${index}-${line}`}><p>{line}</p></article>)}</div>
  </section>;
}
