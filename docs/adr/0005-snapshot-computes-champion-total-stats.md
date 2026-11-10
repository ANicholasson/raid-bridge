# A Snapshot carries each Champion's Total stats, computed by raid-bridge

Each Champion in a Snapshot will carry its Total stats, the eight stats the champion screen shows, with a breakdown by Stat source. raid-bridge computes them from what it reads, because the Game client doesn't keep them for every Champion (decided 2026-11-10). This reverses, for stats only, the rule of the previous phase that a Snapshot holds no computed numbers. Total stats depend on the Account (the Champion's rank, level, ascension, Artifacts and masteries, and the Account's Great Hall and Faction Guardians), so they are the Account's to write. The bonus a Great Hall level or a guardian slot gives is still never written alone.

Every number in the sum comes from the game's static data, read from the Game client like the names are. Only the formula's shape is raid-bridge's own: which sources add flat points and which add a share of Base stats. Where Raid Toolkit's `HeroStatsCalculator` (`raid-toolkit/account-extension`, MIT) has that shape right, it is ported with its notice at the site. No balance table or bonus value is hard-coded. A number the static data doesn't hold goes back to the maintainer as a question.

Total stats mean what the champion screen shows. Bonuses that apply only in battle stay out: leader skills, dungeon and area bonuses, and buffs. Whether the screen counts the Classic Arena league bonus, clan bonuses, the Great Hall and Faction Guardians is settled by research on the screen itself, not assumed here.
<!-- The Arena league and clan bonuses moved from "out" to research: (CodeRabbit, https://github.com/ANicholasson/raid-bridge/pull/6) -->

## Considered Options

- **The Snapshot carries the bonus tables, and whatever reads it does the sum.** This keeps the old rule. It was rejected because it moves the hardest part, shares of Base stats across many sources, to every reader. An AI agent reading a Snapshot gets it wrong or hedges, and the stats it hedged on (Accuracy, Speed) are the ones that decide builds.
- **Read the totals the Game client computed.** This would need no formula, so it is the best source if it exists. The client probably computes stats only for the screen that is open, and a screen's view model is fragile to read. The research ticket that opens the work checks this first: if the client keeps Total stats for every Champion in its model, the reader reads those instead, and this decision is amended.
- **Port Raid Toolkit's calculator as it is.** Rejected. It hard-codes its mastery values and ships its level multiplier as a file, and it leaves out Awakening, blessing, Empowerment, Faction Guardians and each Artifact's ascension bonus.

## Consequences

- Green tests are not enough. Each Stat source is proven against the champion screen of the running Game client, and Total stats match the screen at its rounding on Champions the maintainer picks to cover every source. A mismatch fails the work: there is no "close enough".
- The formula is now raid-bridge's to keep. A game update that changes how a source adds up shows up as a mismatch with the screen, not as a read error, so a stat change in the game's patch notes means checking the screen again.
- Values are written unrounded, in the units Artifacts use: points, and fractions for the critical stats.
- `docs/snapshot.md` says "nothing is computed" and "there are no stats" until the work that writes `stats` lands. That work changes both lines in the same change.
