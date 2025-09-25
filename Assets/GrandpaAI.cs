using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrandpaAI : MonoBehaviour
{
    // gets the new board and returns grandpas stone location (right now it's random, but should be updated in the future
    public Vector2 WarnGrandpa(int[,] grid)
    {
        int x, y;
        while (true)
        {
            x = Random.Range(0, grid.GetLength(0));
            y = Random.Range(0, grid.GetLength(1));

            if (grid[x, y] == 0) 
            {
                Debug.Log("Grandpa: Eat this, dweeb");
                return new Vector2(x, y);
            }
        }
    }

    public void PlayerCheating()
    {
        Debug.Log("Grandpa: Get rid of those extra stones.");
    }
}
