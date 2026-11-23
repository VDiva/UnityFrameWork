using GraphProcessor;
using UnityEditor;
using UnityEngine.UIElements;

namespace FrameWork.InteractiveFilm.Editor
{
    [NodeCustomEditor(typeof(FilmChoiceNode))]
    public sealed class FilmChoiceNodeView : BaseNodeView
    {
        private VisualElement rows;
        private FilmChoiceNode Choice => (FilmChoiceNode)nodeTarget;

        public override void Enable()
        {
            base.Enable();
            rows = new VisualElement();
            rows.style.minWidth = 180f;
            controlsContainer.Add(rows);
            controlsContainer.Add(new Button(AddOption) { text = "+ 添加选项" });
            DrawRows();
        }

        private void AddOption()
        {
            Undo.RegisterCompleteObjectUndo(owner.graph, "添加影游选项");
            Choice.AddOption("新选项");
            MarkChanged();
            DrawRows();
        }

        private void RemoveOption(string id)
        {
            Undo.RegisterCompleteObjectUndo(owner.graph, "删除影游选项");
            Choice.RemoveOption(id);
            MarkChanged();
            DrawRows();
        }

        private void DrawRows()
        {
            rows.Clear();
            for (int i = 0; i < Choice.OptionCount; i++)
            {
                string id = Choice.GetOptionId(i);
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                var field = new TextField($"{i + 1}") { isDelayed = true };
                field.labelElement.style.minWidth = 20f;
                field.labelElement.style.width = 22f;
                field.style.flexGrow = 1f;
                field.SetValueWithoutNotify(Choice.GetLabel(i) ?? "");
                field.RegisterValueChangedCallback(change =>
                {
                    Undo.RegisterCompleteObjectUndo(owner.graph, "编辑影游选项");
                    Choice.RenameOption(id, change.newValue);
                    MarkChanged();
                });
                row.Add(field);
                row.Add(new Button(() => RemoveOption(id)) { text = "−", tooltip = "删除该选项及其连线" });
                rows.Add(row);
            }
        }

        private void MarkChanged()
        {
            EditorUtility.SetDirty(owner.graph);
            owner.graph.NotifyNodeChanged(Choice);
        }
    }
}
