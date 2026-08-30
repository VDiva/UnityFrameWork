项目打开时点击Framework->安装必要包体 项目加载依赖aa包
安装完成后 点击windows->

项目功能


1.资源自动打包
  FrameWork->Asset下发以文件夹为单位会单独构建aa包 如有新放入文件夹的文件点击FrameWork->更新资源会自动创建aa包把文件归属到各自的包里
  之后可以调用api AbMrg->Load<T> 和AbMrg.LoadAsync<T> 同步和异步加载


  
2.Prefab自动生成代码
  Prefab需要放入FrameWork->Asset根据自己需求里面新建文件夹放入预制体
  预制体上含有RectTransform会继承UiActor普通的会继承Actor
  放入后右键FrameWork->预制体->生成代码
  生成的代码会放入FrameWork->Scripts->PrefabSpawnScript文件夹以你预制体的名字进行创建文件夹
  脚本名字也会命名为你预制体的名字
  .Attr是挂载ui的层级一般不是ui可以不用管
  .Awake是走加载预制体组件查询
  .Extend是继承父类的方法然后父类会通过aa包找到预制体自动生成gameobject引用
  .Fun是个空脚本就自己写逻辑的地方 它有mono的生命周期方法重写就好了
  .H是放自动生成文件变量的地方
  如果生成预制体需要那个子集的组件给自己加上AddScripts脚本然后从新FrameWork->Scripts->PrefabSpawnScript就会自动生成脚本引用这个组件变量名为组件名+你的命名


  
3.UI管理器
  同样资源需要放入创建好的FrameWork->Asset
  采用Prefab自动生成代码的方法生成代码
  然后可以通过UiManager.Open<T>进行打开ui T为泛型脚本名
  T需要继承UiActor 你的预制体需要含有RectTransform


  
4.配置表生成代码工具
  xlsx表格放入FrameWork->Xlsx文件夹中 这个文件夹会检测表格变动弹窗让你选择是否生成代码和数据文件
  生成的代码会放在FrameWork->Scripts->Xlsx中 数据文件会存放在FrameWork->Asset->Xlsx中
  会生成类脚本Xlsx_你的表名字 枚举key Xlsx_你的表名字_Key Xlsx_你的表名字_Type 生成枚举需要你的第一个变量是string 还会生成一个查询类Xlsx_你的表名字_Query
  查询类通过key查询获得这个类 然后你就可以读取这个类的数据了 
  配置表规则 第一行为描述 第二行为变量类型 第三行为变量名 下面每一行就是一个类通过第一个变量进行查询


  
5.无限滑动框
  分上下滑动单个和格子布局
  脚本CommonInfiniteScrollView 为上下滑动 所对应的item需要添加上CommonInfiniteScrollCell 设置数据数量CommonInfiniteScrollView.SetCount 可以绑定刷新回调和点击item回调
  脚本CommonGridScrollView 格子布局 所对应的item需要添加上CommonGridScrollCell 方法都和上面的一样


  
6.影游编辑器
  通过xnode+so进行可视化连线控制逻辑走向 内含判断if分支 显示分支等等


  
7.网络联机api通过websocket实现因为我自己做小游戏使用使用websocket
  连接主类WebNet
  WebClientRpcAttribute 给方法打上特性标签即可广播
  WebNetworkIdentity 网络对象id
  WebNetworkAIAuthority 如果运行ai此脚本会根据你是否拥有对象控制权再不同普通上关闭或开启此对象逻辑脚本
  WebNetworkAnimator 动画同步
  WebNetworkBehaviour 网络对象都需要继承这个类
  WebNetworkManager 网络管理器 里面可以生成网络物体销毁网络物体
  WebNetworkRoomManager 房间管理器 房间内数据回调 创建房间退出房间等等api
  WebNetworkSpineAnimator 同步2d骨骼动画
  WebNetworkTransform 同步位置
  WebRoomSharedValues 房间的一些字典数据存储
  
