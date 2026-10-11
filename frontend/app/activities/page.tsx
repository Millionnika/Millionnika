import Link from "next/link";
import { GameNav } from "@/components/GameNav";

export default function ActivitiesPage() {
  return <main><section className="game-panel">
    <GameNav /><Link className="back-link" href="/profile">← Профиль</Link>
    <p className="eyebrow game-eyebrow">Активности</p><h1 className="game-title">Выберите занятие</h1>
    <div className="contract-grid">
      <article className="contract-card"><div><h2>Взлом</h2><p>Киберзадачи и развитие навыка «Взлом».</p></div><Link className="primary-button" href="/hacking">Открыть</Link></article>
      <article className="contract-card"><div><h2>Охота на роботов</h2><p>Пошаговые бои с экипировкой, патронами и отдельным навыком охоты.</p></div><Link className="primary-button" href="/hunting">На охоту</Link></article>
    </div>
  </section></main>;
}
