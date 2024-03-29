using System.Collections.Generic;
using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Perell.Artemis.Generated;

namespace Perell.Artemis
{
    public abstract class PreDictionaryFletcher : ScriptableObject
    {
        [System.Serializable]
        struct ArrowFiringData
        {
            public Arrow arrow;
            public Archer archer;
            public FlagBundle[] importedStates;
            public FlagID[] all;

            public ArrowFiringData(Arrow _arrow, Archer _archer, FlagBundle[] _importedStates, FlagID[] _all)
            {
                arrow = _arrow;
                archer = _archer;
                importedStates = _importedStates;
                all = _all;
            }
        }

        private List<ArrowFiringData> queue;
        public const string CRITERIA_KEY_WORD = "COND";

        private void Awake()
        {
            queue = new List<ArrowFiringData>();
        }

        public bool ProcessArrow(Arrow arrow, Archer sender, FlagBundle[] importedStates, FlagID[] all)
        {
            bool successfullyProcessed = false;

            if (IsBusy())
            {
                if (queue == null)
                {
                    queue = new List<ArrowFiringData>();
                }

                Arrow.HowToHandleBusy decision = arrow.GetWhenBusyDescision();
                ArrowFiringData storedGrouping = new ArrowFiringData(arrow, sender, importedStates, all);

                switch (decision)
                {
                    case Arrow.HowToHandleBusy.CANCEL:
                        successfullyProcessed = false;
                        break;
                    case Arrow.HowToHandleBusy.QUEUE:
                        queue.Add(storedGrouping);
                        successfullyProcessed = true;
                        break;
                    case Arrow.HowToHandleBusy.INTERRUPT:
                        AbruptEnd();
                        Send(arrow.GetArrowID());
                        successfullyProcessed = true;
                        break;
                    case Arrow.HowToHandleBusy.INTERRUPT_CLEAR_QUEUE:
                        AbruptEnd();
                        Send(arrow.GetArrowID());
                        foreach(ArrowFiringData data in queue)
                        {
                            if (data.archer != null)
                            {
                                data.archer.ReturnArrow(data.arrow);
                            }
                        }
                        queue.Clear();
                        successfullyProcessed = true;
                        break;
                    case Arrow.HowToHandleBusy.DELETE:
                        successfullyProcessed = true;
                        break;
                    case Arrow.HowToHandleBusy.FRONT_OF_QUEUE:
                        queue.Insert(0, storedGrouping);
                        successfullyProcessed = true;
                        break;
                    default:
                        Debug.LogError(arrow.name + " has invalid whenAlreadyVoicePlaying value.");
                        successfullyProcessed = false;
                        break;
                }
            }
            else
            {
                Send(arrow.GetArrowID());
                successfullyProcessed = true;
            }

            return successfullyProcessed;
        }

        public void ProcessEnd()
        {
            if (queue.Count > 0)
            {
                ArrowFiringData grouping = queue[0];
                queue.RemoveAt(0);
                if (grouping.arrow.CondtionsMet(grouping.importedStates, grouping.all))
                {
                    Send(grouping.arrow.GetArrowID());
                }
                else
                {
                    if (grouping.archer != null)
                    {
                        grouping.archer.ReturnArrow(grouping.arrow);
                    }
                    ProcessEnd();
                }
            }
        }

        public bool IsSomethingToQueue()
        {
            for(int i = 0; i < queue.Count; i--)
            {
                ArrowFiringData grouping = queue[i];
                if (grouping.arrow.CondtionsMet(grouping.importedStates, grouping.all))
                {
                    return true;
                }
            }
            return false;
        }

        protected abstract void Send(int id);
        protected abstract bool IsBusy();
        protected abstract void AbruptEnd();

#if UNITY_EDITOR
        [ContextMenu("Destroy Database")]
        private void DestroyDatabaseContextMenu()
        {
            DestroyDatabase();
        }

        public abstract void GeneratorArrowDatabase();
        public abstract void DestroyDatabase();
#endif

        public abstract Type GetSymbolType();
    }

    public abstract class Fletcher<T> : PreDictionaryFletcher
    {
        [Header("Database Loading")]
        [SerializeField]
        protected TextAsset csvFile;
        [SerializeField]
        [Min(0)]
        [Tooltip("Number of columns in the CSV used to generate the data structures in each database. Number does not include the base 4 columns.")]
        protected int columnsToReadFrom;
        [SerializeField]
        private SortedStrictDictionary<int,T> database;

        private const int BASE_COLUMNS = 4;
        private const string INTERNAL_SYMBOL_LOCATION = "Assets/Scripts/Generated/Artemis/ArrowEnums/";

        [HideInInspector]
        [SerializeField]
        private InternalSymbolCompiler arrowIDCompiler;
        [HideInInspector]
        [SerializeField]
        private List<string> flagsBeingUsed;
        [HideInInspector]
        [SerializeField]
        private List<int> arrowsNotBeingUsed;
        [HideInInspector]
        [SerializeField]
        private List<string> flagsNoLongerBeingUsed;

        private Bow<T> inSceneObject;

#if UNITY_EDITOR
        public sealed override void GeneratorArrowDatabase()
        {
            //List used to track what Arrows need to be deleted
            arrowsNotBeingUsed = new List<int>();
            if (database != null)
            {
                arrowsNotBeingUsed = database.GetKeyList();
            }

            //List used to track what Flag IDs need to be disconnected
            if(flagsBeingUsed == null)
            {
                flagsBeingUsed = new List<string>();
            }
            flagsNoLongerBeingUsed = new List<string>();
            foreach (string flag in flagsBeingUsed)
            {
                flagsNoLongerBeingUsed.Add(flag);
            }
            flagsBeingUsed.Clear();

            //Reset database
            database = new SortedStrictDictionary<int, T>();

            //Check for folder
            if (!AssetDatabase.IsValidFolder(GetContainingFolder() + "/" + GetArrowFolderName()))
            {
                AssetDatabase.CreateFolder(GetContainingFolder(), GetArrowFolderName());
                AssetDatabase.Refresh();
            }

            //Generate the internalSymbolCompiler for the arrow IDs
            string arrowPrefix = typeof(T).ToString();
            arrowPrefix = arrowPrefix.Replace('.', '_'); //Prevents namespaces from causing compilation errors
            if (arrowIDCompiler == null)
            {
                arrowIDCompiler = new InternalSymbolCompiler(INTERNAL_SYMBOL_LOCATION, arrowPrefix + "_arrows");
            }
            arrowIDCompiler.SetLocation(INTERNAL_SYMBOL_LOCATION, arrowPrefix + "_arrows");

            //Parse CSV
            fgCSVReader.LoadFromString(csvFile.text, BASE_COLUMNS + columnsToReadFrom, AddToDatabase);

            //Delete the unused arrow assets
            string pathOfArrowToDelete;
            foreach (int arrowID in arrowsNotBeingUsed)
            {
                if (arrowIDCompiler != null)
                {
                    arrowIDCompiler.SetToRemove(arrowID);
                }
                pathOfArrowToDelete = GetContainingFolder() + "/" + GetArrowFolderName() + "/" + arrowIDCompiler.FindNameOfValue(arrowID) + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Arrow>(pathOfArrowToDelete) != null)
                {
                    AssetDatabase.DeleteAsset(pathOfArrowToDelete);
                }
            }

            //Disconnect unused flags
            foreach (string flag in flagsNoLongerBeingUsed)
            {
                Goddess.instance.DisconnectFlag(flag, this);
            }

            //Compile arrow and flag IDs
            arrowIDCompiler.WriteFlagEnumScript();
            Goddess.instance.WriteFlagEnumScript();

            EditorUtility.SetDirty(this);

            RespondToFinishedGenerating();
        }

        private void AddToDatabase(Line currentLine)
        {
            bool invalid = false;

            //Arrows must have an ID
            if (currentLine.cell[0] == null || currentLine.cell[0].value == "" || currentLine.cell[0].value == "END" || !IsFlagNameValid(currentLine.cell[0].value))
            {
                if (currentLine.cell[0] == null || currentLine.cell[0].value != "END")
                {
                    if (currentLine.cell[0] != null || !IsFlagNameValid(currentLine.cell[0].value))
                    {
                        Debug.LogError("\""+ currentLine.cell[0].value + "\" is not a valid ID");
                    }
                    else
                    {
                        Debug.LogError("ID was not found");
                    }
                }
                invalid = true;
            }

            //Data intake must be validated
            T data = default(T);
            if (!invalid)
            {
                string[] stringsToInterpret = new string[columnsToReadFrom];

                for (int i = 0; i < columnsToReadFrom; i++)
                {
                    if (currentLine.cell[BASE_COLUMNS + i] != null)
                    {
                        stringsToInterpret[i] = currentLine.cell[BASE_COLUMNS + i].value;
                    }
                    else
                    {
                        stringsToInterpret[i] = "";
                    }
                }

                if (!SetUpDataFromCells(stringsToInterpret, out data))
                {
                    Debug.LogError(data.GetType() + " for " + currentLine.cell[0].value + " was not loaded correctly!");
                    invalid = true;
                }
            }

            //Flag checks must be valid
            SortedStrictDictionary<FlagID, Criterion> _rule = null;
            if(!invalid)
            {
                string flagColumnString = "";
                if(currentLine.cell[2] != null)
                {
                    flagColumnString = currentLine.cell[2].value;
                }

                invalid = !TryEvalFlagList(flagColumnString, out _rule);
            }

            //Valid!!!!
            if (!invalid)
            {
                //1) Add to the official database
                int _id = arrowIDCompiler.FindValueOfString(currentLine.cell[0].value);
                database.Add(_id, data);

                //2) Add/update asset
                PreDictionaryFletcher _systemScriptable = this;
                int _priorityValue = 0;
                Arrow.HowPriorityCalculated _howPriorityCalculated = Arrow.HowPriorityCalculated.SET_VALUE;
                if (currentLine.cell[1] != null)
                {
                    string priorityValueInput = currentLine.cell[1].value.Trim();
                    int indexOf = priorityValueInput.IndexOf(CRITERIA_KEY_WORD);
                    if (indexOf != -1)
                    {
                        if (priorityValueInput.Length == CRITERIA_KEY_WORD.Length)
                        {
                            _howPriorityCalculated = Arrow.HowPriorityCalculated.CRITERIA;
                        }
                        else
                        {
                            indexOf = priorityValueInput.Substring(0, indexOf).LastIndexOf("+");
                            if (int.TryParse(priorityValueInput.Substring(0, indexOf), out _priorityValue))
                            {
                                _howPriorityCalculated = Arrow.HowPriorityCalculated.SUM;
                            }
                            else
                            {
                                _howPriorityCalculated = Arrow.HowPriorityCalculated.CRITERIA;
                            }
                        }
                    }
                    else
                    {
                        int.TryParse(priorityValueInput, out _priorityValue);
                    }
                    
                }

                Arrow.HowToHandleBusy _howToHandleBusy;
                if (currentLine.cell[3] != null)
                {
                    if (!Enum.TryParse<Arrow.HowToHandleBusy>(currentLine.cell[3].value, out _howToHandleBusy))
                    {
                        _howToHandleBusy = Arrow.HowToHandleBusy.CANCEL;
                    }
                }
                else
                {
                    _howToHandleBusy = Arrow.HowToHandleBusy.CANCEL;
                }

                Arrow arrow = AssetDatabase.LoadAssetAtPath<Arrow>(GetContainingFolder() + "/" + GetArrowFolderName() + "/" + currentLine.cell[0].value + ".asset");

                bool exists = arrow != null;

                if (!exists)
                {
                    arrow = ScriptableObject.CreateInstance<Arrow>();
                }

                arrow.Rewrite(_id, _systemScriptable, _priorityValue, _rule, _howToHandleBusy, _howPriorityCalculated);

                if (exists)
                {
                    EditorUtility.SetDirty(arrow);
                }
                else
                {
                    AssetDatabase.CreateAsset(arrow, GetContainingFolder() + "/" + GetArrowFolderName() + "/" + currentLine.cell[0].value + ".asset");
                }

                //3) remove from list of uninvolved Assets for clean up later
                arrowsNotBeingUsed.Remove(_id);
            }
        }

        private bool TryEvalFlagList(string str, out SortedStrictDictionary<FlagID, Criterion> flagChecks)
        {
            bool success = true;
            flagChecks = new SortedStrictDictionary<FlagID, Criterion>();

            string[] inputs = str.Split(',', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < inputs.Length; i++)
            {
                success = TryEvalSpecificFlag(inputs[i], ref flagChecks);
                if(!success)
                {
                    break;
                }
            }

            return success;
        }

        private bool TryEvalSpecificFlag(string input, ref SortedStrictDictionary<FlagID, Criterion> flagChecks)
        {
            //Variable set-up
            CriterionComparisonType compareType = CriterionComparisonType.INVALID;
            Flag.ValueType valueType = Flag.ValueType.INVALID;
            bool valid = false;
            string flag = "";
            string[] tmp;
            float a = 0;
            float b = 0;

            //Trim input
            input = input.Trim();

            //Check input for key symbols
            bool hasLess = input.IndexOf('<') != -1;
            bool hasGreat = input.IndexOf('>') != -1;
            bool hasEq = input.IndexOf('=') != -1;
            bool hasEx = input.IndexOf('!') != -1;

            if (hasGreat)
            {
                valueType = Flag.ValueType.FLOAT;
                valid = !hasLess;
                if (valid)
                {
                    valid = IsValidLessGreat(input, '>', out a, out flag);
                    if(valid)
                    {
                        if (hasEq)
                        {
                            compareType = CriterionComparisonType.GREATER_EQUAL;
                        }
                        else
                        {
                            compareType = CriterionComparisonType.GREATER;
                        }
                    }
                    else
                    {
                        tmp = input.Split('>');
                        valid = tmp.Length == 3; //Check for if it's a range (ex: a >= x >= b)
                        if (valid)
                        {
                            bool hasLeftEq = tmp[1].IndexOf('=') != -1;
                            bool hasRightEq = tmp[2].IndexOf('=') != -1;

                            int leftStartIndex = hasLeftEq ? 1 : 0;
                            int rightStartIndex = hasRightEq ? 1 : 0;

                            if (float.TryParse(tmp[0], out a)
                                && float.TryParse(tmp[2].Substring(rightStartIndex), out b))
                            {
                                flag = tmp[1].Substring(leftStartIndex).Trim();
                                valid = IsFlagNameValid(flag);
                                if (valid)
                                {
                                    //String
                                    string output = a + " >";
                                    if (hasLeftEq)
                                    {
                                        output += "=";
                                    }
                                    output += " " + flag + " >";
                                    if (hasRightEq)
                                    {
                                        output += "=";
                                    }
                                    output += " " + b;
                                    Debug.Log(output);

                                    if (hasLeftEq)
                                    {
                                        if (hasRightEq)
                                        {
                                            compareType = CriterionComparisonType.RANGE_CLOSED;
                                        }
                                        else
                                        {
                                            compareType = CriterionComparisonType.RANGE_CLOSED_OPEN;
                                        }
                                    }
                                    else
                                    {
                                        if (hasRightEq)
                                        {
                                            compareType = CriterionComparisonType.RANGE_OPEN_CLOSED;
                                        }
                                        else
                                        {
                                            compareType = CriterionComparisonType.RANGE_OPEN;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                valid = false;
                            }

                        }
                    }
                }
            }
            else if (hasLess)
            {
                valueType = Flag.ValueType.FLOAT;
                valid = IsValidLessGreat(input, '<', out a, out flag);
                if (valid)
                {
                    if (hasEq)
                    {
                        compareType = CriterionComparisonType.LESS_EQUAL;
                    }
                    else
                    {
                        compareType = CriterionComparisonType.LESS;
                    }
                }
                else
                {
                    tmp = input.Split('<');
                    valid = tmp.Length == 3; //Check for if it's a range (ex: b <= x <= a)
                    if (valid)
                    {
                        bool hasLeftEq = tmp[1].IndexOf('=') != -1;
                        bool hasRightEq = tmp[2].IndexOf('=') != -1;

                        int leftStartIndex = hasLeftEq ? 1 : 0;
                        int rightStartIndex = hasRightEq ? 1 : 0;

                        if (float.TryParse(tmp[0], out b)
                            && float.TryParse(tmp[2].Substring(rightStartIndex), out a))
                        {
                            flag = tmp[1].Substring(leftStartIndex).Trim();
                            valid = IsFlagNameValid(flag);
                            if (valid)
                            {
                                //String
                                string output = b + " <";
                                if (hasLeftEq)
                                {
                                    output += "=";
                                }
                                output += " " + flag + " <";
                                if (hasRightEq)
                                {
                                    output += "=";
                                }
                                output += " " + a;
                                Debug.Log(output);

                                if (hasLeftEq)
                                {
                                    if (hasRightEq)
                                    {
                                        compareType = CriterionComparisonType.RANGE_CLOSED;
                                    }
                                    else
                                    {
                                        compareType = CriterionComparisonType.RANGE_OPEN_CLOSED;
                                    }
                                }
                                else
                                {
                                    if (hasRightEq)
                                    {
                                        compareType = CriterionComparisonType.RANGE_CLOSED_OPEN;
                                    }
                                    else
                                    {
                                        compareType = CriterionComparisonType.RANGE_OPEN;
                                    }
                                }
                            }
                        }
                        else
                        {
                            valid = false;
                        }

                    }
                }
            }
            else if (hasEq)
            {
                tmp = input.Split('=');

                valid = !hasEx && tmp.Length == 2;

                if (valid)
                {
                    if (float.TryParse(tmp[1], out a)) //number value
                    {
                        valueType = Flag.ValueType.FLOAT;
                        flag = tmp[0].Trim();
                        valid = IsFlagNameValid(flag);
                        if (valid)
                        {
                            compareType = CriterionComparisonType.EQUALS;
                            Debug.Log(flag + " = " + a);
                        }
                    }
                    else
                    {
                        valueType = Flag.ValueType.SYMBOL;
                        flag = tmp[0].Trim();
                        string enumPossibly = tmp[1].Trim();
                        valid = IsFlagNameValid(flag) && IsFlagNameValid(enumPossibly);
                        if (valid)
                        {
                            FlagID flagID = Goddess.instance.ConnectFlag(flag, Flag.ValueType.SYMBOL, this);

                            if (flagID != FlagID.INVALID)
                            {
                                a = (float)Goddess.instance.FindSymbolValueOfFlag(flagID, enumPossibly);
                                compareType = CriterionComparisonType.EQUALS;
                                Debug.Log(flag + " = " + enumPossibly);
                            }
                            else
                            {
                                valid = false;
                            }
                        }
                    }
                }
            }
            else if (hasEx)
            {
                valueType = Flag.ValueType.BOOL;
                valid = input.IndexOf('!') == 0
                    && input.LastIndexOf('!') == 0;

                if (valid)
                {
                    flag = input.Substring(1);
                    a = 0;
                    compareType = CriterionComparisonType.EQUALS;

                    valid = IsFlagNameValid(flag);
                    if (valid)
                    {
                        Debug.Log(flag + " = FALSE");
                    }
                }
            }
            else
            {
                valueType = Flag.ValueType.BOOL;
                valid = IsFlagNameValid(input);
                if (valid)
                {
                    flag = input;
                    a = 1;
                    compareType = CriterionComparisonType.EQUALS;
                    Debug.Log(flag + " = TRUE");
                }
            }

            if (valid)
            {
                valid = ProcessCriterion(flag, valueType, compareType, a, b, ref flagChecks);
            }
            
            if(!valid)
            {
                Debug.LogError("\"" + input + "\" was found INVALID");
            }

            return valid;
        }

        private bool IsValidLessGreat(string input, char compareChar, out float a, out string flag)
        {
            bool valid;
            flag = "";
            a = 0;

            bool hasEq = input.IndexOf('=') != -1;
            bool hasEx = input.IndexOf('!') != -1;

            valid = !hasEx;

            if (valid)
            {
                string[] tmp = input.Split(compareChar);
                valid = tmp.Length == 2;
                if (valid)
                {
                    int startIndex = hasEq ? 1 : 0;
                    if (float.TryParse(tmp[1].Substring(1), out a))
                    {
                        flag = tmp[0].Trim();
                        valid = IsFlagNameValid(flag);
                        if (valid)
                        {
                            string output = flag + " " + compareChar;
                            if (hasEq)
                            {
                                output += "=";
                            }
                            output += " " + a;

                            Debug.Log(output);
                        }
                    }
                    else
                    {
                        valid = false;
                    }
                }
            }

            return valid;
        }

        private bool IsFlagNameValid(string flag)
        {
            bool valid = true;

            char[] arr = flag.ToCharArray();

            if (char.IsLetter(arr[0]))
            {
                foreach (char e in arr)
                {
                    if (!char.IsLetterOrDigit(e) && e != '_')
                    {
                        valid = false;
                        break;
                    }
                }
            }
            else
            {
                valid = false;
            }

            return valid;
        }

        private bool ProcessCriterion(string flag, Flag.ValueType valueType, CriterionComparisonType comparisonType, float a, float b, ref SortedStrictDictionary<FlagID, Criterion> flagChecks)
        {
            bool success = true;

            FlagID flagID = Goddess.instance.ConnectFlag(flag, valueType, this);

            if (flagID != FlagID.INVALID)
            {
                flagChecks.Add(flagID, new Criterion(flagID, comparisonType, a, b));
                flagsBeingUsed.Add(flag.ToUpper());
                flagsNoLongerBeingUsed.Remove(flag.ToUpper());
            }
            else
            {
                success = false;
            }

            return success;
        }

        private string GetContainingFolder()
        {
            string rtn = AssetDatabase.GetAssetPath(this);
            rtn = rtn.Substring(0, rtn.LastIndexOf('/'));
            return rtn;
        }

        private string GetArrowFolderName()
        {
            return name + " Arrows";
        }

        public Arrow[] RetrieveAllGeneratedArrows()
        {
            string arrowLocation = GetContainingFolder() + "/" + GetArrowFolderName() + "/";
            string[] assets = AssetDatabase.FindAssets("t:Perell.Artemis.Arrow", new string[] { arrowLocation });
            List<Arrow> arrows = new List<Arrow>();
            foreach (string asset in assets) 
            {
                arrows.Add(AssetDatabase.LoadAssetAtPath<Arrow>(AssetDatabase.GUIDToAssetPath(asset)));
            }
            Debugging.ArtemisDebug.Instance.OpenReportLine("RetrieveAllGenArrows").Report(arrows).CloseReport();
            return arrows.ToArray();
        }

        protected virtual void RespondToFinishedGenerating()
        {
            //This is for if the player needs to edit the database once the Arrows assets have been generated
        }

        public sealed override void DestroyDatabase()
        {
            //List used to track what Arrows need to be deleted
            arrowsNotBeingUsed = new List<int>();
            if (database != null)
            {
                arrowsNotBeingUsed = database.GetKeyList();
            }

            //List used to track what Flag IDs need to be disconnected
            if (flagsBeingUsed == null)
            {
                flagsBeingUsed = new List<string>();
            }
            flagsNoLongerBeingUsed = new List<string>();
            foreach (string flag in flagsBeingUsed)
            {
                flagsNoLongerBeingUsed.Add(flag);
            }
            flagsBeingUsed.Clear();

            //Reset database
            database = new SortedStrictDictionary<int, T>();

            //Delete the unused arrow assets
            //string pathOfArrowToDelete;
            //foreach (int arrowID in arrowsNotBeingUsed)
            //{
            //    if (arrowIDCompiler != null)
            //    {
            //        arrowIDCompiler.SetToRemove(arrowID);
            //    }
            //    pathOfArrowToDelete = GetContainingFolder() + "/" + GetArrowFolderName() + "/" + arrowIDCompiler.FindNameOfValue(arrowID) + ".asset";
            //    if (AssetDatabase.LoadAssetAtPath<Arrow>(pathOfArrowToDelete) != null)
            //    {
            //        AssetDatabase.DeleteAsset(pathOfArrowToDelete);
            //    }
            //}
            arrowIDCompiler.DeleteFlagEnumScript();
            AssetDatabase.DeleteAsset(GetContainingFolder() + "/" + GetArrowFolderName() + "/");


            //Disconnect unused flags
            foreach (string flag in flagsNoLongerBeingUsed)
            {
                Goddess.instance.DisconnectFlag(flag, this);
            }

            //Compile arrow and flag IDs
            arrowIDCompiler.WriteFlagEnumScript();
            Goddess.instance.WriteFlagEnumScript();

            EditorUtility.SetDirty(this);
        }
#endif

        public bool FindData(int id, out T value)
        {
            value = default(T);
            bool success = database.HasKey(id);
            if (success)
            {
                value = database[id];
            }
            return success;
        }

        public void SetInSceneObject(Bow<T> _value)
        {
            inSceneObject = _value;
        }

        public Bow<T> GetInSceneObject()
        {
            return inSceneObject;
        }

        protected sealed override void Send(int id)
        {
            T value;
            if (FindData(id, out value))
            {
                inSceneObject.Send(value);
            }
        }

        protected sealed override bool IsBusy()
        {
            return inSceneObject.IsBusy();
        }

        protected sealed override void AbruptEnd()
        {
            inSceneObject.AbruptEnd();
        }

        protected abstract bool SetUpDataFromCells(string[] dataToInterpret, out T valueDetermined);

        public override Type GetSymbolType()
        {
            return arrowIDCompiler.GetEnumType();
        }
    }
}
