import Link from "next/link";

const links = [
  ["/profile", "Профиль"],
  ["/contracts", "Контракты"],
  ["/activities", "Активности"],
  ["/implant-center", "Тренировка"],
  ["/shop", "Магазин"],
  ["/inventory", "Инвентарь"],
  ["/players", "Игроки"],
  ["/friends", "Друзья"],
] as const;

export function GameNav() {
  return <nav className="game-nav" aria-label="Игровая навигация">
    {links.map(([href, label]) => <Link href={href} key={href}>{label}</Link>)}
  </nav>;
}
