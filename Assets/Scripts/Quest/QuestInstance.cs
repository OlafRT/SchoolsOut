// QuestInstance.cs
using System;
using System.Collections.Generic;

[Serializable]
public class QuestInstance
{
    public QuestDefinition def;
    public List<int> progress = new(); // mirrors def.objectives

    public QuestInstance(QuestDefinition d){
        def = d;
        progress.Clear();
        for (int i=0;i<d.objectives.Count;i++) progress.Add(0);
    }

    public bool IsComplete {
        get{
            for (int i=0;i<def.objectives.Count;i++){
                var o = def.objectives[i];
                if (o.type == QuestDefinition.ObjectiveSpec.Type.Reach ||
                    o.type == QuestDefinition.ObjectiveSpec.Type.Talk)
                {
                    if (progress[i] < 1) return false;
                }
                else
                {
                    if (progress[i] < o.requiredCount) return false;
                }
            }
            return true;
        }
    }
}
