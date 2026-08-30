using System;

namespace FrameWork.Script.WebNet
{
    /// <summary>标记需要自动同步的字段。fieldId 在同一个脚本类型内必须唯一且不能随意修改。</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class WebSyncVarAttribute : Attribute
    {
        public uint FieldId { get; }
        public string Hook { get; set; }

        public WebSyncVarAttribute(uint fieldId)
        {
            if (fieldId == 0) throw new ArgumentOutOfRangeException(nameof(fieldId));
            FieldId = fieldId;
        }
    }
}
