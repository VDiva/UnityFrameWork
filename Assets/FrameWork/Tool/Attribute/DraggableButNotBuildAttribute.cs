using System;
using UnityEngine;

// 自定义特性：标记「可拖拽、打包不包含、打包后自动恢复引用」的变量
[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public class DraggableButNotBuildAttribute : PropertyAttribute
{
}