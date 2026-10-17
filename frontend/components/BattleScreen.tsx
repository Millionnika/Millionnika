"use client";

import { useEffect, useRef, useState } from "react";

export type BattleWeapon = {
  slot: string;
  name: string;
  ammoInClip: number;
  reserveAmmo: number;
  clipSize: number;
};

export type BattleParticipant = {
  side: "player" | "opponent";
  name: string;
  type: "player" | "robot";
  health: number;
  maximumHealth: number;
};

export type BattleEquipment = {
  slot: string;
  type: "weapon" | "armor";
  name: string;
  damage: number;
  accuracy: number;
  armor: number;
  coverage: Record<string, number>;
};

export type BattleProtection = {
  source: "natural";
  armor: number;
  coverage: Record<string, number>;
};

type BattleScreenProps = {
  status: string;
  turn: number;
  maximumTurns: number;
  expiresAtUtc: string;
  nextActionAtUtc: string | null;
  playerHealth: number;
  maximumPlayerHealth: number;
  opponentHealth: number;
  maximumOpponentHealth: number;
  weapons: BattleWeapon[];
  participants?: BattleParticipant[];
  playerEquipment?: BattleEquipment[];
  opponentEquipment?: BattleEquipment[];
  opponentProtection?: BattleProtection[];
  log: string[];
  busy: boolean;
  onAction: (action: "attack" | "reload", slot: string) => void;
  onExpired: () => void;
};

export function BattleScreen({
  status, turn, maximumTurns, expiresAtUtc, nextActionAtUtc,
  playerHealth, maximumPlayerHealth, opponentHealth, maximumOpponentHealth,
  weapons, participants, playerEquipment, opponentEquipment, opponentProtection,
  log, busy, onAction, onExpired,
}: Readonly<BattleScreenProps>) {
  const now = useClock(status === "active");
  const secondsLeft = secondsUntil(expiresAtUtc, now);
  const cooldown = nextActionAtUtc ? secondsUntil(nextActionAtUtc, now) : 0;
  const active = status === "active";
  const expirationRequested = useRef<string | null>(null);
  const expiring = active && secondsLeft === 0;
  const player = participants?.find((value) => value.side === "player");
  const opponent = participants?.find((value) => value.side === "opponent");

  useEffect(() => {
    if (!expiring || expirationRequested.current === expiresAtUtc) return;
    expirationRequested.current = expiresAtUtc;
    onExpired();
  }, [expiresAtUtc, expiring, onExpired]);

  return <section className={`battle-screen ${active ? "active" : "finished"}`} aria-label="Экран боя" aria-busy={busy}>
    <header className="battle-summary">
      <div><span>Состояние</span><strong>{expiring ? "Проверяем завершение" : active ? "Бой продолжается" : "Бой завершён"}</strong></div>
      <div><span>Ход</span><strong>{turn} / {maximumTurns}</strong></div>
      <div><span>{active ? "До завершения" : "Время"}</span><strong>{expiring ? "завершается…" : active ? formatDuration(secondsLeft) : "Завершено"}</strong></div>
      <div className="battle-action-state" aria-live="polite"><span>Действие</span><strong>{active ? expiring ? "ожидание результата" : busy ? "выполняется…" : cooldown > 0 ? `через ${cooldown} с` : "доступно" : "недоступно"}</strong></div>
    </header>

    <div className="battle-participants">
      <Combatant name={player?.name ?? "Вы"} health={player?.health ?? playerHealth} maximumHealth={player?.maximumHealth ?? maximumPlayerHealth} side="player" equipment={playerEquipment} weapons={weapons} actionDisabled={busy || expiring || cooldown > 0} onAction={onAction} />
      <div className="battle-versus" aria-hidden="true">VS</div>
      <Combatant name={opponent?.name ?? "Противник"} health={opponent?.health ?? opponentHealth} maximumHealth={opponent?.maximumHealth ?? maximumOpponentHealth} side="opponent" equipment={opponentEquipment} protection={opponentProtection} />
    </div>

    <div className="battle-lower">
      <BattleLog log={log} playerEquipment={playerEquipment} opponentEquipment={opponentEquipment} />
      <BattleRoster participants={participants} playerFallback={{ name: "Вы", health: playerHealth, maximumHealth: maximumPlayerHealth }} opponentFallback={{ name: "Противник", health: opponentHealth, maximumHealth: maximumOpponentHealth }} />
    </div>
  </section>;
}

function Combatant({ name, health, maximumHealth, side, equipment, protection, weapons, actionDisabled = true, onAction }: Readonly<{ name: string; health: number; maximumHealth: number; side: "player" | "opponent"; equipment?: BattleEquipment[]; protection?: BattleProtection[]; weapons?: BattleWeapon[]; actionDisabled?: boolean; onAction?: (action: "attack" | "reload", slot: string) => void }>) {
  const percent = maximumHealth > 0 ? Math.max(0, Math.min(100, health / maximumHealth * 100)) : 0;
  return <article className={`battle-combatant ${side}`}>
    <div className="combatant-heading"><strong>{name}</strong><span>{health} / {maximumHealth}</span></div>
    <div className="battle-health-track" role="progressbar" aria-label={`Здоровье: ${name}`} aria-valuemin={0} aria-valuemax={maximumHealth} aria-valuenow={health}>
      <i style={{ width: `${percent}%` }} />
    </div>
    <div className="combatant-layout">
      <div className="combatant-figure-wrap">
        <div className="combatant-figure" aria-hidden="true"><i className="figure-head" /><i className="figure-body" /><i className="figure-arm left" /><i className="figure-arm right" /><i className="figure-leg left" /><i className="figure-leg right" /></div>
        {protection?.some((item) => item.source === "natural") && <div className="natural-protection"><span>Естественная защита:</span><strong>{formatProtection(protection)}</strong></div>}
      </div>
      <EquipmentColumn title="Броня" slots={armorSlots} equipment={equipment} side={side} />
      <EquipmentColumn title="Оружие" slots={weaponSlots} equipment={equipment} side={side} weapons={weapons} actionDisabled={actionDisabled} onAction={onAction} />
    </div>
    <span className="combatant-health-label">Здоровье {Math.round(percent)}%</span>
  </article>;
}

const slotNames: Record<string, string> = {
  primary: "Основное", secondary: "Вторичное", melee: "Ближний бой", temporary: "Временное", weapon: "Оружие",
  helmet: "Голова", body: "Корпус", pants: "Ноги", gloves: "Перчатки", boots: "Обувь",
};
const weaponSlots = ["primary", "secondary", "melee", "temporary"];
const armorSlots = ["helmet", "body", "pants", "gloves", "boots"];

function EquipmentColumn({ title, slots, equipment, side, weapons, actionDisabled = true, onAction }: Readonly<{ title: string; slots: string[]; equipment?: BattleEquipment[]; side: "player" | "opponent"; weapons?: BattleWeapon[]; actionDisabled?: boolean; onAction?: (action: "attack" | "reload", slot: string) => void }>) {
  const kind = title === "Оружие" ? "weapons" : "armour";
  const relevant = equipment?.filter((item) => item.type === (title === "Оружие" ? "weapon" : "armor")) ?? [];
  const hasSnapshot = equipment !== undefined;
  return <div className={`combatant-equipment ${kind}`} aria-label={title}>
    <strong>{title}</strong>
    {slots.map((slot, index) => {
      const item = relevant.find((entry) => entry.slot === slot) ?? (title === "Оружие" && index === 0 ? relevant.find((entry) => entry.slot === "weapon") : undefined);
      const weapon = weapons?.find((entry) => entry.slot === slot);
      if (kind === "weapons" && side === "player") return <PlayerWeaponSlot key={slot} slot={slot} item={item} weapon={weapon} hasSnapshot={hasSnapshot} disabled={actionDisabled} onAction={onAction} />;
      return <div className={`combatant-slot${item ? " filled" : " empty"}`} key={slot}>
        <span>{slotNames[slot] ?? slot}</span><i aria-hidden="true">{kind === "weapons" ? "W" : "A"}</i><b title={item?.name}>{item?.name ?? (hasSnapshot ? "Пусто" : "Нет данных старого боя")}</b>
      </div>;
    })}
  </div>;
}

function PlayerWeaponSlot({ slot, item, weapon, hasSnapshot, disabled, onAction }: Readonly<{ slot: string; item?: BattleEquipment; weapon?: BattleWeapon; hasSnapshot: boolean; disabled: boolean; onAction?: (action: "attack" | "reload", slot: string) => void }>) {
  const temporary = slot === "temporary";
  const firearm = Boolean(weapon && weapon.clipSize > 0);
  const reloadReason = weapon && firearm
    ? weapon.ammoInClip >= weapon.clipSize ? "Магазин уже полный" : weapon.reserveAmmo === 0 ? "Запас патронов пуст" : ""
    : "Перезарядка не требуется";
  const attackReason = temporary
    ? "Действие для временного оружия пока не поддерживается"
    : !item ? hasSnapshot ? "Слот пуст" : "Нет данных старого боя"
    : !weapon ? "Сервер не вернул состояние оружия"
    : firearm && weapon.ammoInClip === 0 ? "Магазин пуст — перезарядите оружие" : "";
  const blockedReason = disabled ? "Дождитесь доступности действия" : attackReason;
  return <div className={`combatant-slot player-weapon-slot${item ? " filled" : " empty"}${temporary ? " future" : ""}`}>
    <button className="weapon-slot-attack" disabled={disabled || Boolean(attackReason)} title={blockedReason} onClick={() => onAction?.("attack", slot)}>
      <span>{slotNames[slot] ?? slot}</span><i aria-hidden="true">{temporary ? "…" : "W"}</i><b title={item?.name}>{item?.name ?? (hasSnapshot ? "Пусто" : "Нет данных старого боя")}</b>
      {weapon && <small>{firearm ? `${weapon.ammoInClip}/${weapon.clipSize} · запас ${weapon.reserveAmmo}` : "Без патронов"}</small>}
      {attackReason && <em>{attackReason}</em>}
    </button>
    {weapon && firearm && !temporary && <button className="slot-reload" disabled={disabled || Boolean(reloadReason)} title={reloadReason} onClick={() => onAction?.("reload", slot)}>Перезарядить</button>}
  </div>;
}

function BattleRoster({ participants, playerFallback, opponentFallback }: Readonly<{ participants?: BattleParticipant[]; playerFallback: Omit<BattleParticipant, "side" | "type">; opponentFallback: Omit<BattleParticipant, "side" | "type"> }>) {
  const sides = {
    player: participants?.filter((item) => item.side === "player") ?? [],
    opponent: participants?.filter((item) => item.side === "opponent") ?? [],
  };
  if (sides.player.length === 0) sides.player.push({ ...playerFallback, side: "player", type: "player" });
  if (sides.opponent.length === 0) sides.opponent.push({ ...opponentFallback, side: "opponent", type: "robot" });
  return <section className="battle-roster" aria-labelledby="battle-roster-title">
    <div className="battle-section-heading"><div><span>Состав</span><h2 id="battle-roster-title">Участники боя</h2></div></div>
    <div className="battle-roster-list">{[...sides.player, ...sides.opponent].map((item, index) => <RosterRow participant={item} index={index} key={`${item.side}-${item.name}-${index}`} />)}</div>
  </section>;
}

function RosterRow({ participant: item, index }: Readonly<{ participant: BattleParticipant; index: number }>) {
  const percent = item.maximumHealth > 0 ? Math.max(0, Math.min(100, item.health / item.maximumHealth * 100)) : 0;
  return <article className={item.side}>
    <div className="roster-avatar" aria-hidden="true">{item.name.slice(0, 1).toUpperCase()}</div>
    <div><span>{item.type === "player" ? "Игрок" : "Робот"}</span><strong>{item.name}</strong><div className="roster-health" role="progressbar" aria-label={`Здоровье: ${item.name}`} aria-valuemin={0} aria-valuemax={item.maximumHealth} aria-valuenow={item.health}><i style={{ width: `${percent}%` }} /></div></div>
    <b>{item.health}/{item.maximumHealth}</b><span className="visually-hidden">Участник {index + 1}</span>
  </article>;
}

function BattleLog({ log, playerEquipment, opponentEquipment }: Readonly<{ log: string[]; playerEquipment?: BattleEquipment[]; opponentEquipment?: BattleEquipment[] }>) {
  return <details className="battle-log" open>
    <summary><span>Журнал боя</span><small>{log.length}</small></summary>
    {log.length === 0 ? <p className="battle-log-empty">Действий пока нет.</p> : <ol>
      {log.slice().reverse().map((line, index) => <li className={logEntrySide(line, playerEquipment, opponentEquipment)} key={`${log.length - index}-${line}`}><span>Ход {log.length - index}</span><p>{line}</p></li>)}
    </ol>}
  </details>;
}

function logEntrySide(line: string, playerEquipment?: BattleEquipment[], opponentEquipment?: BattleEquipment[]) {
  if (playerEquipment?.some((item) => line.startsWith(item.name))) return "player";
  if (opponentEquipment?.some((item) => line.startsWith(item.name))) return "opponent";
  return "neutral";
}

function formatProtection(protection: BattleProtection[]) {
  const armor = Math.max(...protection.map((item) => item.armor));
  return armor;
}

function useClock(running: boolean) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (!running) return;
    const timer = window.setInterval(() => setNow(Date.now()), 250);
    return () => window.clearInterval(timer);
  }, [running]);
  return now;
}

function secondsUntil(value: string, now: number) {
  return Math.max(0, Math.ceil((new Date(value).getTime() - now) / 1000));
}

function formatDuration(seconds: number) {
  const minutes = Math.floor(seconds / 60);
  return `${minutes}:${String(seconds % 60).padStart(2, "0")}`;
}
