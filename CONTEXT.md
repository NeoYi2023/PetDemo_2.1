# CONTEXT.md — PetDemo 领域词汇表

> 这是**词汇表，仅此而已**。不包含实现细节、规格草稿或实现决策。
> 这些内容属于 `SPEC_FarmBattleDemo.md` 和 `.codebuddy/plans/`。
>
> 每个 AI Agent 会话开始时，应读取本文件以确保使用统一语言。

---

## Glossary

### Role

**中文**：玩家操控的主角，显示名与剧情名均为 Role。  
**English**：The player-controlled protagonist; display and narrative name is "Role".  
**Code**：`PlayerRole` / `PlayerActor`（避免与引擎保留词冲突）

---

### FarmSim

**中文**：农场模拟模块，负责种植、生长、收获与资源产出。  
**English**：Farm simulation module handling planting, growth, harvest, and resource output.  
**Code**：`FarmSim`

---

### TurnBattle

**中文**：回合战斗模块，按回合顺序结算行动。  
**English**：Turn-based combat module where actions are resolved in turn order.  
**Code**：`TurnBattle`

---

### RoleState

**中文**：Role 的跨场景属性与背包简化状态，在 FarmSim 和 TurnBattle 之间共享。  
**English**：Cross-scene Role stats and simplified inventory, shared between FarmSim and TurnBattle.  
**Code**：`RoleState`

---

### Tier-1 Attributes（一阶属性）

**中文**：基础战斗常量，直接参与伤害计算与出手顺序。  
**English**：Base combat constants driving damage and turn order.  
**Code**：`atk`, `def`, `maxHp`, `currentHp`, `agility`

---

### Tier-2 Attributes（二阶属性）

**中文**：施加方触发率，取值范围 `0..1`。  
**English**：Attacker-side trigger rates, range `0..1`.  
**Code**：`critRate`, `comboRate`, `counterRate`, `blockRate`

---

### Tier-3 Attributes（三阶属性）

**中文**：承受方抵消率，与二阶字段一一对应，取值范围 `0..1`。  
**English**：Defender-side resistances paired one-to-one with Tier-2 fields, range `0..1`.  
**Code**：`critResist`, `comboResist`, `counterResist`, `blockResist`

---

### Design Baseline（设计基准分辨率）

**中文**：竖屏逻辑设计画布 1080×1920，所有 UI 以此为准。  
**English**：Portrait logical canvas 1080×1920; all UI follows this baseline.  
**Code**：Canvas Reference Resolution = (1080, 1920)

---

### Safe Area（安全区）

**中文**：避开刘海、圆角与系统手势带的可交互内容区域。  
**English**：Interactive content region avoiding notches, rounded corners, and system gesture areas.  
**Code**：`Screen.safeArea`

---

### Seam（接缝）

**中文**：可在不编辑该处的情况下改变行为的位置（Michael Feathers 概念），用于模块边界设计和测试注入点。  
**English**：A place where behavior can be changed without editing that spot; used for module boundary design and test injection.  
**Code**：接口参数、`virtual` 方法、事件委托等

---

## 维护规范

- 新术语**确认后立即添加**，不批量积累
- 每个术语条目格式：`### <Name>` + 中文 + English + Code
- 本文件不得包含：函数签名、实现步骤、具体文件路径、版本历史
- 与 `SPEC_FarmBattleDemo.md §Glossary` 保持一致，若有冲突以本文件为准（本文件更细粒度）
