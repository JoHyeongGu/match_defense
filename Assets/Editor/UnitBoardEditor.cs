using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using MatchDefense.Defense;

[CustomEditor(typeof(UnitBoard))]
public class UnitBoardEditor : Editor
{
    private SerializedProperty pathMaterialProp;
    private SerializedProperty pathWidthProp;
    private SerializedProperty yOffsetProp;

    private void OnEnable()
    {
        // UnitBoard.cs에 선언된 변수들을 찾아서 연결합니다.
        pathMaterialProp = serializedObject.FindProperty("pathMaterial");
        pathWidthProp = serializedObject.FindProperty("pathWidth");
        yOffsetProp = serializedObject.FindProperty("yOffset");
    }

    public override void OnInspectorGUI()
    {
        // 인스펙터의 변경 사항을 추적하기 시작합니다.
        serializedObject.Update();

        UnitBoard board = (UnitBoard)target;

        EditorGUI.BeginChangeCheck();
        int newX = EditorGUILayout.IntField("Grid Size X", board.gridSizeX);
        int newY = EditorGUILayout.IntField("Grid Size Y", board.gridSizeY);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(board, "Change Grid Size");
            board.gridSizeX = Mathf.Max(1, newX);
            board.gridSizeY = Mathf.Max(1, newY);
            ResizeArray(board);
        }

        if (board.spawnableCells == null || board.spawnableCells.Length != board.gridSizeX * board.gridSizeY)
        {
            ResizeArray(board);
        }

        if (board.pathCells == null)
        {
            board.pathCells = new List<int>();
        }

        // --- 새로 추가된 Path 옵션 UI ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Path Mesh Settings", EditorStyles.boldLabel);

        // 이 코드가 인스펙터에 Material 등 설정 옵션을 그려줍니다.
        EditorGUILayout.PropertyField(pathMaterialProp);
        EditorGUILayout.PropertyField(pathWidthProp);
        EditorGUILayout.PropertyField(yOffsetProp);
        // ---------------------------------

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Board Configuration", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Click: Toggle Spawnable (Green)\nDouble Click: Toggle Path (Blue)", MessageType.Info);

        EditorGUI.BeginChangeCheck();

        GUIStyle cellStyle = new GUIStyle(GUI.skin.box);
        cellStyle.alignment = TextAnchor.MiddleCenter;
        cellStyle.normal.textColor = Color.white;
        cellStyle.fontStyle = FontStyle.Bold;

        for (int y = board.gridSizeY - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < board.gridSizeX; x++)
            {
                int index = y * board.gridSizeX + x;
                Rect rect = GUILayoutUtility.GetRect(25, 25, GUILayout.Width(25), GUILayout.Height(25));

                int pathOrder = board.pathCells.IndexOf(index);
                bool isSpawnable = board.spawnableCells[index];

                Color originalColor = GUI.backgroundColor;

                if (pathOrder >= 0)
                    GUI.backgroundColor = Color.blue;
                else if (isSpawnable)
                    GUI.backgroundColor = Color.green;
                else
                    GUI.backgroundColor = Color.gray;

                string labelText = pathOrder >= 0 ? (pathOrder + 1).ToString() : "";
                GUI.Box(rect, labelText, cellStyle);

                GUI.backgroundColor = originalColor;

                Event e = Event.current;
                if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
                {
                    if (e.button == 0)
                    {
                        Undo.RecordObject(board, "Modify Board Cell");
                        if (e.clickCount == 2)
                        {
                            if (pathOrder >= 0)
                            {
                                board.pathCells.Remove(index);
                            }
                            else
                            {
                                board.pathCells.Add(index);
                                board.spawnableCells[index] = false;
                            }
                            e.Use();
                        }
                        else if (e.clickCount == 1)
                        {
                            if (pathOrder < 0)
                            {
                                board.spawnableCells[index] = !board.spawnableCells[index];
                            }
                            e.Use();
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(board);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Clear Path"))
        {
            Undo.RecordObject(board, "Clear Path");
            board.pathCells.Clear();
            EditorUtility.SetDirty(board);
        }

        if (GUILayout.Button("Generate Path Mesh (3D Line)"))
        {
            Undo.RecordObject(board, "Generate Path Mesh");
            board.GeneratePathMesh();
            EditorUtility.SetDirty(board);
        }

        // 인스펙터에서 변경된 Material 등의 값을 실제 객체에 적용합니다.
        serializedObject.ApplyModifiedProperties();
    }

    private void ResizeArray(UnitBoard board)
    {
        bool[] newArray = new bool[board.gridSizeX * board.gridSizeY];
        if (board.spawnableCells != null)
        {
            int minX = Mathf.Min(board.gridSizeX, board.gridSizeX);
            int minY = Mathf.Min(board.gridSizeY, board.gridSizeY);

            for (int y = 0; y < minY; y++)
            {
                for (int x = 0; x < minX; x++)
                {
                    int oldIndex = y * board.gridSizeX + x;
                    int newIndex = y * board.gridSizeX + x;
                    if (oldIndex < board.spawnableCells.Length && newIndex < newArray.Length)
                    {
                        newArray[newIndex] = board.spawnableCells[oldIndex];
                    }
                }
            }
        }
        board.spawnableCells = newArray;

        if (board.pathCells != null)
        {
            for (int i = board.pathCells.Count - 1; i >= 0; i--)
            {
                if (board.pathCells[i] >= newArray.Length)
                {
                    board.pathCells.RemoveAt(i);
                }
            }
        }
    }
}