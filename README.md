# THINKING IT

A first-person psychological horror game in Unity, built on the "white bear" experiment:
the harder you try not to think about something, the more you think about it.

**"It" is drawn to whatever you pay attention to.** Looking makes things real. Reading about it makes it stronger.
Closing your eyes is the only way to calm down, but then you're blind.

## Chapters
1. **Arrival** — the abandoned Halvard Institute at night. Find the three missing fuses (patient ward, offices, a hidden isolation room), restore the power, find Teo's recorder, cross the dark observation hall.
2. **The Waiting Room** — an apartment that loops twelve times. Find what changed each time.
3. **The Archive** — recover six session tapes across the stacks, the vault, the listening room and the deep stacks. Cross the dark with your eyes closed.
4. **The Lake House** — Mara's memory of the lake. Look at six things from that afternoon until they're real, then walk down to the dock.
5. **The White Bear Room** — pass three trials without looking at the white bear, open the chamber, and choose what to do with the thought.

Chapters continue automatically: the end screen counts down 10 seconds and loads the next one.
Full playthrough: roughly 2.5–3 hours.

Three endings: **Forget**, **Remember**, and a hidden one (**Think It**) for players who read every forbidden document.

## Controls
| Key | Action |
|---|---|
| WASD / arrows | move |
| Mouse | look |
| E | interact / read |
| Hold SPACE (or Q) | close your eyes |
| ESC / P | pause |
| F9 | skip to the next chapter (testing) |

Hold your gaze on unfinished things (outlined doorways, missing planks) to make them real.

## Running it
- Unity **6000.6.2f1** (Unity 6), Universal Render Pipeline, new Input System.
- Open the project, then open `Assets/Scenes/LoadingScene.unity` and press Play
  (or use **Tools > Thinking It > Play From Start**).
- The first time the scripts load, **Tools > Thinking It > Setup Project** runs by itself: it creates material
  templates and a texture library in `Assets/Resources/ThinkingIt` and puts the scenes in the build list.

## How it's built
Everything is generated in code at runtime (levels, meshes, textures, sounds, UI), so the scenes only contain one
`SceneBoot` object each.

```
Assets/Scripts/ThinkingIt/
  Core/      meshes, painted textures, fonts, materials, dust
  Audio/     synthesized sound effects, loops and the title music
  UI/        HUD, documents, pause/end screens, intro cards
  Game/      Game (shared rules: gaze, focus, dread, "it"), World (level grid), data (all the writing)
  Chapters/  Chapter1..Chapter5: one file per chapter
  Title/     title screen
  Editor/    project setup tool
```

Photo textures in `Assets/grafics/textures` are used when present; otherwise everything falls back to textures painted in code.
