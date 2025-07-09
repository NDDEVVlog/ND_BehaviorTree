// --- START OF FILE BoolArray2D.cs ---
using System;
using UnityEngine;
using System.Collections.Generic;

[Serializable]
public class BoolArray2D : ISerializationCallbackReceiver
{
    // We must serialize Rows and Columns to be available after deserialization.
    [SerializeField]
    private int rows;
    [SerializeField]
    private int columns;

    [SerializeField]
    private bool[] dataArray;

    // The 2D array is now a runtime convenience, not the source of truth for serialization.
    public bool[,] Values { get; private set; }

    public int Rows 
    { 
        get => rows; 
        private set => rows = value; 
    }
    public int Columns 
    { 
        get => columns; 
        private set => columns = value; 
    }

    public BoolArray2D(int rows, int columns)
    {
        this.Rows = rows;
        this.Columns = columns;
        this.Values = new bool[rows, columns];
        // Initialize dataArray based on the new dimensions
        dataArray = new bool[rows * columns];
    }

    // Indexer for easy access: myBoolArray[row, col]
    public bool this[int row, int col]
    {
        get { return Values[row, col]; }
        set { Values[row, col] = value; }
    }

    public bool ValueAt(int row, int column)
    {
        return Values[row, column];
    }

    public void Resize(int newRows, int newColumns)
    {
        bool[,] newValues = new bool[newRows, newColumns];
        if (Values != null)
        {
            for (int i = 0; i < Math.Min(newRows, this.Rows); i++)
            {
                for (int j = 0; j < Math.Min(newColumns, this.Columns); j++)
                {
                    newValues[i, j] = Values[i, j];
                }
            }
        }
        
        Values = newValues;
        this.Rows = newRows;
        this.Columns = newColumns;
        dataArray = new bool[newRows * newColumns]; // Also resize the 1D array
        OnBeforeSerialize(); // Sync changes to dataArray
    }
    
    public void Rotate(bool clockwise)
    {
        // Create new array with swapped dimensions
        bool[,] newValues = new bool[Columns, Rows];
        int newRows = Columns;
        int newCols = Rows;

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                if (clockwise)
                {
                    newValues[c, newCols - 1 - r] = Values[r, c];
                }
                else
                {
                    newValues[newRows - 1 - c, r] = Values[r, c];
                }
            }
        }

        // Update properties and data
        Values = newValues;
        this.Rows = newRows;
        this.Columns = newCols;
        OnBeforeSerialize();
    }


    public Tuple<int, int>[] FindTrueValues()
    {
        var trueValues = new List<Tuple<int, int>>();
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                if (Values[row, column])
                {
                    trueValues.Add(new Tuple<int, int>(row, column));
                }
            }
        }
        return trueValues.ToArray();
    }

    public void OnBeforeSerialize()
    {
        if (dataArray == null || dataArray.Length != Rows * Columns)
        {
            dataArray = new bool[Rows * Columns];
        }

        if (Values == null || Values.GetLength(0) != Rows || Values.GetLength(1) != Columns)
        {
            // If Values is out of sync, let's not risk an error. This can happen on first init.
            return;
        }

        int index = 0;
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                dataArray[index++] = Values[row, column];
            }
        }
    }

    public void OnAfterDeserialize()
    {
        // Now that Rows and Columns are serialized, this works correctly.
        if (rows > 0 && columns > 0)
        {
            Values = new bool[rows, columns];
            if (dataArray != null && dataArray.Length == rows * columns)
            {
                int index = 0;
                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        Values[row, column] = dataArray[index++];
                    }
                }
            }
        }
    }

    // SetValue is no longer needed if serialization is correct.
    // If you need it for other purposes, it should be:
    public void SetValuesFrom1D(List<bool> boolArray, int numRows, int numCols)
    {
        if (boolArray.Count != numRows * numCols) return;
        Resize(numRows, numCols);
        dataArray = boolArray.ToArray();
        OnAfterDeserialize();
    }
}