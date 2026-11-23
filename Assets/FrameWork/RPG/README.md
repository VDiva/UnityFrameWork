# RPG 战斗模块

适用于项目当前的 Unity Input Manager。模块位于 `FrameWork.FPS` 程序集和同名命名空间中。其他 asmdef 中的代码如需直接引用这些类型，可在其 References 中加入 `FrameWork.FPS`。

## 架构编辑面板

在 Unity 中打开 `FrameWork → RPG → 架构编辑面板`。架构总览显示战斗、背包道具和角色成长的关系。场景组件页显示当前已加载场景的 RPG 组件，SO 配置页列出项目中的武器、弹种、道具和等级曲线。搜索并选中条目后，可在右侧直接编辑 Inspector 字段，也可以定位到 Hierarchy 或 Project 窗口。

点击 **新建武器** 创建武器 SO。选中武器后，指定场景中的 `FpsLoadout`，再点 **添加武器到装备栏**。面板会在武器挂点下创建并注册一个武器节点，可撤销。将武器模型挂到该节点下，并按场景需要调整枪口和命中层。

在同一配置中可绑定已有的 Animator Controller，也可点 **创建并绑定动画树模板**。先设置武器类型，模板会按类型生成状态。创建后，可从面板打开 Unity Animator 并为状态分配动作 Clip。

在角色的 Animator 所在节点挂上 `FpsWeaponAnimator`。组件会自动寻找父节点的 `FpsLoadout`，也可以手动指定。换武器时自动应用其动画树。枪械模板使用 `Equip`、`Fire`、`Reload` Trigger，近战模板使用 `Equip`、`Melee` Trigger。自定义动画树可沿用同名参数。需要按动作帧结算换弹时，在 Reload 动画的装弹帧添加调用 `CompleteReloadFromAnimation` 的 Animation Event，再将枪械 SO 的 Reload Completion 改为 External。

## 运行示例

打开 `Example/FpsTrainingRange.unity` 并进入 Play Mode。场景默认使用俯视视角。WASD 按屏幕方向移动，角色转向鼠标在地面上的位置，左键朝该方向攻击。按 1、2、3 分别装备卡宾枪、手枪和训练剑。测试训练剑时，走到靶子附近再按左键。出生点左前方是弹药包，右前方是治疗包，走近即可拾取。按 F 补充当前武器的备弹，按 H 治疗。示例玩家初始生命值为 60，便于直接测试治疗。

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
