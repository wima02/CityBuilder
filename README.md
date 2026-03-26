# CityBuilder

A gridless city-builder in the style of *Town to City*, built with **Godot 4 + C# + Custom ECS**.
Mediterranean voxel aesthetic. Cozy, stress-free, no punishing economy.

---

## Quick Start

1. Open the project in Godot 4.3+
2. Add `Scripts/Main.cs` as an **AutoLoad** in Project → Settings → AutoLoad
3. Press **F5** to run — the ECS boots, runs self-tests, and loads the main menu

---

## Project Structure

```
CityBuilder/
├── .cursorrules          ← AI coding rules (Cursor AI reads this automatically)
├── src/
│   ├── ECS/
│   │   ├── GameConstants.cs      ← All constants — no magic numbers
│   │   ├── EntityManager.cs      ← Entity CRUD + component queries
│   │   └── World.cs              ← ECS singleton, spawns test scene
│   ├── Components/
│   │   └── Components.cs         ← All component structs (pure data)
│   ├── Systems/
│   │   ├── VoxelRenderSystem.cs        ← Phase 1: MultiMesh voxel renderer
│   │   ├── SplineRoadSystem.cs         ← Phase 2: Bezier road placement
│   │   ├── PlacementSystem.cs          ← Phase 2: Mouse → world, preview
│   │   ├── OrientationAndSnappingSystem.cs  ← Phase 2: road alignment + snapping
│   │   ├── PathfindingSystem.cs        ← Phase 3: A* on virtual grid
│   │   ├── AgentSystems.cs             ← Phase 3: spawn + movement
│   │   ├── NeedsAndHappinessSystems.cs ← Phase 3: needs / beauty / happiness
│   │   └── EconomySystems.cs           ← Phase 4: supply chain, labor, research
│   ├── StateMachines/
│   │   └── GameStateManager.cs   ← Hierarchical state machine
│   ├── Utils/
│   │   └── ObjectPool.cs         ← Generic pool for agents + road nodes
│   └── Main.cs                   ← AutoLoad entry point, wires all systems
├── tests/
│   ├── ECSTests.cs               ← EntityManager unit tests
│   └── SystemTests.cs            ← System logic unit tests (no scene needed)
├── scenes/
│   ├── ui/                       ← MainMenu.tscn, BuildMode.tscn, ...
│   └── world/                    ← Main.tscn, CityBuilder.tscn
├── assets/
│   ├── voxels/                   ← BoxMesh placeholders (Phase 1)
│   ├── textures/                 ← Road, building, deco materials
│   ├── audio/                    ← BGM, SFX
│   └── ui/                       ← Icons, fonts
└── docs/
    ├── DIA_Cycle.md              ← Design → Implement → Analyze workflow
    ├── PRD_Templates.md          ← Feature spec templates
    └── AntiPatterns.md           ← What NOT to do
```

---

## Architecture Rules (non-negotiable)

| Rule | Detail |
|------|--------|
| **ECS** | Every object = entity (uint). Components = pure data structs. Systems = stateless logic. |
| **SRP** | One system = one responsibility. New feature = new system OR new component, never both. |
| **Events** | Systems communicate via C# events, never directly. |
| **Performance** | Struct components, MultiMesh for voxels, object pooling for agents. |
| **File size** | Max 1000 lines per file — split if larger. |

---

## Development Phases

| Phase | Status | Systems |
|-------|--------|---------|
| 1 — ECS Foundation | ✅ Done | World, EntityManager, VoxelRenderSystem, GameStateManager |
| 2 — Gridless Building | 🔨 Current | SplineRoadSystem, PlacementSystem, OrientationSystem, SnappingSystem |
| 3 — Citizen Agents | 📋 Planned | AgentSpawner, Pathfinding, NeedsCycle, Happiness, Beauty |
| 4 — Economy & Research | 📋 Planned | SupplyChain, Labor, Research, TownUpgrade |

---

## DIA Cycle

Every feature follows **Design → Implement → Analyze**:

1. **Design** — write a 3–5 sentence PRD, list affected components/systems
2. **Implement** — Layer 1: data model → Layer 2: logic system → Layer 3: integration
3. **Analyze** — suggest 2–3 unit tests, name one performance gotcha, give a next step

---

## Cursor AI Workflow

The `.cursorrules` file is automatically loaded by Cursor AI.
Use these ready-made prompts to drive each step:

**Phase 2 – Step A (roads):**
> `"Start Phase 2 Step A: refine SplineRoadSystem. The Bezier mesh looks rough — add UV mapping and smooth normals. Show code in max 50-line blocks."`

**Phase 3 – Step A (agents):**
> `"Start Phase 3 Step A: wire AgentSpawnerSystem to a TrainStation entity. Citizens should walk to their assigned Dwelling via PathfindingSystem.RequestPath(). Show integration in Main.cs."`

---

## Running Tests

Tests run automatically in debug builds on startup.
To run manually, add `ECSTests` and `SystemTests` as AutoLoad nodes,
or call `ECSTests.RunAll()` / `SystemTests.RunAll()` from any script.
