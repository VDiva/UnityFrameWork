using UnityEngine;
using XNode;
using System;
using System.Linq;
using Sirenix.OdinInspector; // 引入 Linq 用于快速查询

public class BaseNode : Node {

    // 序列化存储 ID，但在 Inspector 中隐藏，避免手动修改破坏唯一性
    [ReadOnly] 
    public string uniqueID;

    // // 公开属性供外部读取
    // public string UniqueID {
    //     get { return uniqueID; }
    // }

    // xNode 的初始化方法（在创建、加载、复制时都会调用）
    protected override void Init() {
        base.Init();
        
        // 确保 ID 存在且唯一
        UpdateIDInGraph();
    }

    private void UpdateIDInGraph() {
        // 情况 1: ID 为空（刚创建的新节点）
        bool isInvalid = string.IsNullOrEmpty(uniqueID);
        
        // 情况 2: ID 不为空，但 Graph 中已有其他节点使用了这个 ID（通常发生在复制粘贴时）
        // 注意：graph.nodes 包含当前节点自己，所以要排除自己 (n != this)
        if (!isInvalid) {
            isInvalid = graph.nodes.Any(n => 
                n != null && 
                n != this && 
                n is BaseNode baseNode && 
                baseNode.uniqueID == this.uniqueID
            );
        }

        // 如果无效，生成新的
        if (isInvalid) {
            uniqueID = Guid.NewGuid().ToString();
            // 在编辑器下标记为已修改，确保 ID 被保存
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}