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
  participants: BattleParticipant[];
  log: string[];
  busy: boolean;
  onAction: (action: "attack" | "reload", slot: string) => void;
  onExpired: () => void;
};

export function BattleScreen({
  status, turn, maximumTurns, expiresAtUtc, nextActionAtUtc,
  playerHealth, maximumPlayerHealth, opponentHealth, maximumOpponentHealth,
  weapons, participants, log, busy, onAction, onExpired,
}: Readonly<BattleScreenProps>) {
  const now = useClock(status === "active");
  const secondsLeft = secondsUntil(expiresAtUtc, now);
  const cooldown = nextActionAtUtc ? secondsUntil(nextActionAtUtc, now) : 0;
  const active = status === "active";
  const expirationRequested = useRef<string | null>(null);
  const expiring = active && secondsLeft === 0;
  const player = participants.find((value) => value.side === "player");
  const opponent = participants.find((value) => value.side === "opponent");

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
      <Combatant name={player?.name ?? "Вы"} health={player?.health ?? playerHealth} maximumHealth={player?.maximumHealth ?? maximumPlayerHealth} side="player" />
      <div className="battle-versus" aria-hidden="true">VS</div>
      <Combatant name={opponent?.name ?? "Противник"} health={opponent?.health ?? opponentHealth} maximumHealth={opponent?.maximumHealth ?? maximumOpponentHealth} side="opponent" />
    </div>

    <section className="battle-loadout" aria-labelledby="battle-weapons-title">
      <div className="battle-section-heading">
        <div><span>Ваше снаряжение</span><h2 id="battle-weapons-title">Оружие и действия</h2></div>
        {active && cooldown > 0 && <small>Следующее действие будет доступно через {cooldown} с.</small>}
      </div>
      <div className="battle-weapons">
        {weapons.map((weapon) => <WeaponActions
          key={weapon.slot}
          weapon={weapon}
          disabled={busy || !active || expiring || cooldown > 0}
          active={active}
          onAction={onAction}
        />)}
      </div>
    </section>

    <section className="battle-log" aria-labelledby="battle-log-title">
      <div className="battle-section-heading">
        <div><span>Хронология</span><h2 id="battle-log-title">Журнал боя</h2></div>
        <small>{log.length} {log.length === 1 ? "запись" : "записей"}</small>
      </div>
      {log.length === 0 ? <p className="battle-log-empty">Действий пока нет.</p> :
        <ol>{log.slice().reverse().map((line, index) => <li key={`${log.length - index}-${line}`}><span>{log.length - index}</span><p>{line}</p></li>)}</ol>}
    </section>
  </section>;
}

function Combatant({ name, health, maximumHealth, side }: Readonly<{ name: string; health: number; maximumHealth: number; side: "player" | "opponent" }>) {
  const percent = maximumHealth > 0 ? Math.max(0, Math.min(100, health / maximumHealth * 100)) : 0;
  return <article className={`battle-combatant ${side}`}>
    <div className="combatant-heading"><strong>{name}</strong><span>{health} / {maximumHealth}</span></div>
    <div className="battle-health-track" role="progressbar" aria-label={`Здоровье: ${name}`} aria-valuemin={0} aria-valuemax={maximumHealth} aria-valuenow={health}>
      <i style={{ width: `${percent}%` }} />
    </div>
    <div className="combatant-figure" aria-hidden="true"><i className="figure-head" /><i className="figure-body" /><i className="figure-arm left" /><i className="figure-arm right" /><i className="figure-leg left" /><i className="figure-leg right" /></div>
    <span className="combatant-health-label">Здоровье {Math.round(percent)}%</span>
  </article>;
}

function WeaponActions({ weapon, disabled, active, onAction }: Readonly<{ weapon: BattleWeapon; disabled: boolean; active: boolean; onAction: (action: "attack" | "reload", slot: string) => void }>) {
  const firearm = weapon.clipSize > 0;
  const reloadReason = weapon.ammoInClip >= weapon.clipSize
    ? "Магазин уже полный."
    : weapon.reserveAmmo === 0 ? "Запас патронов пуст." : "";
  const attackReason = firearm && weapon.ammoInClip === 0
    ? "Магазин пуст — перезарядите оружие или выберите другое." : "";

  return <article className="battle-weapon-card">
    <div className="battle-weapon-icon" aria-hidden="true">{firearm ? "W" : "✦"}</div>
    <div className="battle-weapon-info">
      <span>{weapon.slot}</span><strong>{weapon.name}</strong>
      {firearm ? <div className="ammo-status">
        <span><b>{weapon.ammoInClip}</b> / {weapon.clipSize}<small>магазин</small></span>
        <span><b>{weapon.reserveAmmo}</b><small>запас</small></span>
      </div> : <p>Боеприпасы не нужны</p>}
      {active && attackReason && <small className="battle-action-note error">{attackReason}</small>}
      {active && firearm && reloadReason && <small className="battle-action-note">Перезарядка недоступна: {reloadReason}</small>}
    </div>
    <div className="battle-weapon-actions">
      <button className="primary-button" disabled={disabled || Boolean(attackReason)} title={attackReason} onClick={() => onAction("attack", weapon.slot)}>Атака</button>
      {firearm && <button className="secondary-button" disabled={disabled || Boolean(reloadReason)} title={reloadReason} onClick={() => onAction("reload", weapon.slot)}>Перезарядить</button>}
    </div>
  </article>;
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
