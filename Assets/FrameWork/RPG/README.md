# RPG 战斗模块

适用于项目当前的 Unity Input Manager。模块位于 `FrameWork.FPS` 程序集和同名命名空间中。其他 asmdef 中的代码如需直接引用这些类型，可在其 References 中加入 `FrameWork.FPS`。

## 快速开始

1. 在 Unity 中选择 **FrameWork → RPG → 打开 Ember Vault 示例**，进入 Play Mode，体验守卫战斗、道具补给、经验和关卡撤离。[Ember Vault 场景](Example/Dungeon/EmberVault.unity)
2. 单独调试射击、近战、换弹或视角时，打开练习场。默认使用俯视视角，角色面向鼠标瞄准方向。按 V 可切换第一人称和第三人称。[练习场场景](Example/FpsTrainingRange.unity)
3. 选择 **FrameWork → RPG → 架构编辑面板**，在 **资产配置** 中选择武器、子弹或敌人，再创建或编辑对应类型。将武器加到场景中的装备栏后，可直接进入 Play Mode 验证参数。
4. 选择 **FrameWork → RPG → 行为树编辑器** 打开敌人绑定的图，沿着高亮节点和连线查看执行顺序，在侧栏核对感知距离、当前目标和自定义参数。

武器 Animator Controller 负责装备、射击、挥砍和换弹动作。敌人 Behavior Tree 负责要在什么时候执行追击、攻击或返回等逻辑。前者绑定到武器配置，后者绑定到 `RpgEnemyAgent`。

## 架构编辑面板

在 Unity 中打开 `FrameWork → RPG → 架构编辑面板`。架构总览显示战斗、背包道具、角色成长和武器动画的关系。场景组件页显示当前已加载场景的 RPG 组件。资产配置页把武器分为枪械和近战，把子弹与弹药分为弹种、武器引用的弹丸预制体和弹药包，其他道具和等级曲线另有分组。搜索并选中条目后，可在右侧使用 Odin 编辑配置，或定位到 Hierarchy／Project 窗口。

在资产配置页选择类型后，新建按钮会切换为该类的创建菜单。例如 **新建武器** 可创建枪械或近战 SO，**新建子弹** 可创建弹丸预制体、弹种或弹药包。新建的弹丸预制体可在武器 SO 中直接引用。

选中资产后，右侧可以预览已绑定的图标和预制体模型。三个练习场武器配置都绑定了可预览模型。选中武器后，指定场景中的 `FpsLoadout`，再点 **添加武器到装备栏**。面板会创建武器节点、挂上已绑定的模型并注册到装备栏，操作可撤销。可按场景需要调整枪口和命中层。

选中武器 SO 或场景武器时，右侧顶部的 **攻击参数** 可直接编辑伤害、射程和攻击间隔。近战武器还显示判定半径，已绑定弹丸的枪械还显示弹丸速度。场景武器绑定了 SO 时，修改会应用到共享该配置的所有武器。选中弹丸预制体也能查看并编辑引用它的武器参数。

在同一配置中可绑定已有的 Animator Controller，也可点 **创建并绑定动画树模板**。先设置武器类型，模板会按类型生成状态。创建后，可从面板打开 Unity Animator 并为状态分配动作 Clip。

在角色的 Animator 所在节点挂上 `FpsWeaponAnimator`。组件会自动寻找父节点的 `FpsLoadout`，也可以手动指定。换武器时自动应用其动画树。枪械模板使用 `Equip`、`Fire`、`Reload` Trigger，近战模板使用 `Equip`、`Melee` Trigger。自定义动画树可沿用同名参数。需要按动作帧结算换弹时，在 Reload 动画的装弹帧添加调用 `CompleteReloadFromAnimation` 的 Animation Event，再将枪械 SO 的 Reload Completion 改为 External。

## 运行示例

**Ember Vault** 是一张独立的样板关卡。通过 **FrameWork → RPG → 打开 Ember Vault 示例** 打开场景并进入 Play Mode。击败两名守卫后，地牢门会打开。走到门后青色区域完成撤离。失败或通关后按 Enter 重开。守卫使用现有可视化行为树，关卡复用弹药、治疗包、武器栏和经验系统。

复制该场景制作关卡时，可在 `RpgEncounterGate` 中指定必须击败的敌人和对应的门，撤离位置由示例的 Extraction 节点决定。布景在进入 Play Mode 后生成，模型来自 Kenney 的 [Mini Dungeon](https://kenney.nl/assets/mini-dungeon)，授权文件保存在 `Example/Dungeon`。

示例中的玩家和守卫使用 FBX 骨骼动作，移动时在待机、行走和奔跑之间切换。开火、挥砍和换弹由现有武器事件驱动，受伤时会闪红，守卫倒地动作播放后再隐藏。示例的动作代码位于 `Example/Dungeon`，其他关卡也可以继续使用自己的 Animator Controller。

原练习场 `Example/FpsTrainingRange.unity` 适合单独测试武器。两张场景默认使用俯视视角，WASD 移动，鼠标瞄准，左键攻击。1、2、3 分别装备卡宾枪、手枪和训练剑，R 换弹，F 补给，H 治疗，V 切换视角。

## 场景接入

按 V 在俯视、第一人称和第三人称之间循环，也可调用 `FpsPlayer.SetViewMode(mode)`。俯视模式可以配置相机距离、俯角和水平朝向。第三人称可以配置肩后距离、镜头偏移、镜头碰撞层和角色模型。镜头碰撞层应排除玩家自己的 Collider。把武器挂点放在玩家根节点下，并绑定到 Weapon Aim Pivot，使武器跟随瞄准方向，同时保持在角色身上。

1. 创建玩家根节点，添加 `FpsPlayer`。Unity 会自动添加 `CharacterController`。按角色尺寸设置胶囊，常用起点是高度 2、半径 0.4、Center Y 为 1。
2. 将 Camera 放到玩家的子节点，相机局部 Y 可从 1.6 开始。把 Camera 拖到 `FpsPlayer`。按下面的方式配置武器栏，或直接在 `FpsPlayer.Weapon` 绑定一把武器。
3. 创建带 Collider 的地面和目标。给目标添加 `FpsHealth`。目标的 Collider 可在其子节点上。用 Inspector 的 On Depleted 事件配置销毁、隐藏或记分逻辑。
4. 给本地玩家的 Collider 设置专用 Layer，并从武器的 Hit Layers 中排除这一层。保留墙和目标所在层，射线会在第一个命中的实体 Collider 停止。

进入 Play Mode 后，左键捕获鼠标。WASD 移动，鼠标瞄准，左 Shift 冲刺，空格跳跃，左键开火，R 换弹，Esc 释放鼠标。松开鼠标控制后，再点击可恢复控制。

## 枪械

1. 在 Project 窗口中用 `Create → FrameWork → FPS → Ammo Type` 创建弹种，再用 `Weapon` 创建枪械配置。配置包含名称、图标、弹种、射击模式、弹匣容量、备弹上限、初始备弹、射速间隔、换弹时长、射程和伤害。
2. 给枪械预制体根节点添加 `FpsWeapon`，绑定 Definition，并按场景配置 Hit Layers。同一配置可以被多个武器实例共享。每个实例独立保存弹匣和备弹。
3. 给玩家根节点添加 `FpsLoadout`。在玩家下创建 Weapon Mount，绑定到 Loadout。将初始武器以同级节点放在该挂点下，加入 Weapons 列表。数字键 1–9 切换对应槽位。切换时保留弹量，正在进行的换弹会取消。

枪械配置中的 Projectile Prefab 可绑定带 `FpsProjectile` 的预制体，Projectile Speed 设置弹丸飞行速度。可在武器的 Muzzle 字段绑定枪口节点。练习场的卡宾枪已配置弹丸，手枪保留即时射线模式。

### 自动换弹与动画

打开枪械 SO 的 Auto Reload 后，最后一发射出且仍有备弹时会自动发起换弹。手动按 R 也走同一个换弹流程。Reload Completion 默认为 Timed，等待 Reload Duration 后补充弹匣。

接入人物换弹动画时，将 Reload Completion 改为 External。订阅 `FpsWeapon.ReloadStarted`，记录发出事件的武器和本次请求编号，并播放动作。动画到达装弹完成帧时，由 Animation Event 或动画控制器对原武器调用 `TryCompleteReload(requestId)`。切枪取消换弹后，旧请求或重复回调会返回 `false`，备弹量保持原样。

## 道具与拾取

枪械和近战武器都可用 Weapon 道具拾取，共用武器栏和受击接口。近战武器在 Weapon 配置中将 Kind 设为 Melee，通过 Shot Interval、Range、Melee Radius 和 Damage 设置攻击间隔、距离、判定半径和伤害。可订阅 `FpsWeapon.MeleeSwung` 播放挥砍动画。
1. 在 `Create → FrameWork → FPS → Items` 下创建 Ammo、Health 或 Weapon。每种道具可配置名称、图标和携带上限。弹药配置填写弹种和补充量，治疗配置填写回血量，武器道具绑定已配置的枪械预制体。
2. 给玩家根节点添加 `FpsInventory`。需要治疗时也在根节点添加 `FpsHealth`。在场景中给道具对象添加 `FpsItemPickup`，绑定道具和数量，确认 Sphere Collider 勾选 Is Trigger，Rigidbody 勾选 Is Kinematic 并关闭 Use Gravity。
3. 玩家接触拾取物后，库存有空间时拾取相应数量，剩余数量留在场景中。给 `FpsPlayer.Quick Item` 绑定快捷道具后可按 F 使用，也可由 UI 调用 `FpsInventory.TryUse(item)`。治疗只对有伤且存活的玩家起效。弹药包补给当前同弹种武器，达到上限时停止补给。武器道具在武器栏还有空槽且未拥有该枪型时入栏。效果生效后消耗一个道具。

UI 可订阅 `FpsLoadout.EquippedChanged`、`FpsInventory.Changed`、`FpsWeapon.AmmoChanged` 和 `FpsHealth.HealthChanged`，并用 `GetCount(item)` 查询库存。命中效果可订阅 `FpsWeapon.Hit`，开火动画和音效可订阅 `FpsWeapon.Fired`。自定义道具可继承 `FpsItemDefinition`，自定义受击对象可实现 `IFpsDamageable`。

## 经验与等级

通过 `Create → FrameWork → RPG → Level Curve` 创建等级曲线。每一项是升到下一级所需的经验，曲线有几项就能升几次级。给玩家挂上 `RpgProgression` 并绑定曲线，给目标挂上 `RpgExperienceReward` 并填写击败奖励。攻击方经验由 `FpsHealth.Defeated` 的攻击者参数归属，枪弹和近战共用这条流程。

练习场每个靶子提供 50 XP，击败两个靶子即可升到 2 级。HUD 显示当前等级和升级进度。界面可以订阅 `RpgProgression.Changed`，属性或解锁系统可以订阅 `LevelChanged`。任务等其他系统可直接调用 `GainExperience`。

## 敌人

练习场右前方有一个陪练机器人。进入感知范围会被追击，离开追击范围后它会返回出生点。击败机器人可获得 100 XP。将 `Example/Sparring Bot.prefab` 拖入新场景即可复用配置。

在架构面板中通过 **资产配置 → 敌人 → 新建敌人** 创建行为 SO，可设置感知距离、追击半径、攻击距离、移速和转身速度。给敌人挂上 `RpgEnemyAgent`，将现有的近战或枪械 `FpsWeapon` 绑定到 Weapon，并按场景设置视线层与武器命中层。单玩家场景可自动寻找玩家，生成系统也可以调用 `SetTarget` 指定目标。需要经验奖励时挂上 `RpgExperienceReward`。

敌人使用 CharacterController 直接追击，攻击会检查视线遮挡。需要在复杂地图中绕路时，可把寻路作为独立扩展接入。

### 可视化行为树

在 **FrameWork → RPG → 行为树编辑器** 中创建或打开行为树，也可以从架构面板的 **资产配置 → 敌人 → 新建敌人 → 行为树** 创建。新建资产带有攻击、追击和返回出生点的示例连线。将资产绑定到敌人组件的 **Behavior Tree** 字段后，该敌人按图运行。练习场和陪练机器人预制体已绑定 `Example/Sparring Bot Behavior Tree.asset`。

在画布空白处按 Space 添加节点，从父节点的输出端口拉线到子节点的父节点端口。选择和顺序节点按 **1–4** 端口顺序执行。选择节点尝试第一个未失败的分支，顺序节点依次执行直至子节点失败或仍在运行，反转节点交换成功与失败。条件和动作可直接在节点上选择。运行时每帧从入口重新评估，靠近后可从追击切换到攻击。多个敌人可以共用同一树，移动和战斗状态保存在各自的组件上。

在 RPG 架构面板选中行为树或已绑定行为树的敌人后，点击 **新建动作脚本** 生成并打开 C# 文件。在 `Execute(agent, deltaTime)` 中实现动作，默认模板演示调用内置待机逻辑。保存并等待 Unity 编译后，从 **插入已有动作 → 自定义** 选择新动作并连线，也可以在图上按 Space 从 **行为树 → 自定义动作** 添加。

自定义动作可复用 `CheckCondition` 和 `RunBuiltIn`，也可通过 `agent` 调用自己的组件。树资源可由多个敌人共享，计时器等运行状态要放在敌人实例的组件上。脚本放在其他自定义程序集时，该程序集需要引用 `FrameWork.FPS` 和 NodeGraphProcessor Runtime。

编辑完成后使用 **检查连线** 和 **保存**。校验覆盖入口数量、重复父节点、缺失子节点、未连接节点和环。未绑定行为树的敌人仍使用默认行为。绑定了无效行为树的敌人会待机，并在 Console 显示校验原因。

### 运行调试

进入 Play Mode 后打开行为树，在右上角 **行为树运行调试** 选择要追踪的敌人。画布会标出该敌人最近一帧执行过的节点及连线，并在节点标题上标注执行序号。蓝色表示进行中，绿色表示成功，红色表示失败。可以用 Unity 的暂停按钮停在某一帧查看。

侧栏显示当前 AI 状态、目标、目标距离、感知／追击／攻击范围、生命值、武器以及这一帧判断过的条件。自定义动作可调用 `agent.ReportBehaviorParameter("参数名", value)`，将当前动画阶段、计时或其他值显示在侧栏。
