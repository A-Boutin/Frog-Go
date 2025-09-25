using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using static UnityEditor.PlayerSettings;
using Random = UnityEngine.Random;

public class Board : MonoBehaviour
{
    //stones
    enum Stones : int
    {
        Empty = 0,         // 000000
        Black = 1,         // 000001
        White = 2,         // 000010
        Marker = 4,        // 000100
        Liberty = 8,       // 001000
        BlackPoint = 16,   // 010000
        WhitePoint = 32,   // 100000
        NULL = 63,         // 111111
    }

    public GameObject whiteStone;
    public GameObject blackStone;

    public GameObject ghostStone;
    private GameObject ghostStoneInstance;

    //start with a 9x9 grid, later we can implement bigger board versions
    static int boardSize = 9;
    int[,] grid = new int[boardSize,boardSize];
    GameObject[,] stones = new GameObject[boardSize, boardSize];

    private Vector3 boardOffset = new Vector3(-4.5f, 0, -4.5f);
    private Vector3 pieceOffset = new Vector3(0.5f, 0, 0.5f);

    private Vector2 mouseOver;

    private static Vector2 notPos = new Vector2(-1, -1);
    private Vector2[] prevMove = { notPos, notPos };

    private int passCount = 0;

    public TurnManager turnManager;
    public TurnTimer turnTimer;

    public GrandpaAI grandpaAI;

    //keep a list of played stones to ensure we're sticking with the rules (to keep track of cheating)
    public List<Vector2> playedStones = new List<Vector2>();

    //keep a list of removed stones to ensure we're sticking with the rules (not removing ones we shouldn't)
    public List<Vector2> removedStones = new List<Vector2>();

    private void Start()
    {
        ghostStoneInstance = Instantiate(ghostStone);

        for(int i =0; i < boardSize; i++)
        {
            for(int j=0; j < boardSize; j++)
            {
                grid[j, i] = (int)Stones.Empty;
            }
        }
    }

    private void Update()
    {
        if (!isGameOver())
        {
            UpdateMouseOver();

            if (turnManager.isWhiteTurn())
            {
                if(passCount > 0) { Pass();  }
                else
                {
                    // Board must first check if player is cheating
                    // If so, grampa will tell them to get rid of the extra stones, or whatever else they did to cheat
                    // should also have a list of removed stones
                    // If player removes stones they shouldn't then grampa will scold them for that too
                    if(playedStones.Count > 1)
                    {
                        grandpaAI.PlayerCheating();

                        turnTimer.ResetTimer();
                        turnTimer.PauseTimer();
                    }
                    else
                    {
                        //grampa putting stones in a location that was previously removed by the player removes it from the removed list
                        Vector2 newStone = grandpaAI.WarnGrandpa(grid);
                        Vector3 spawnLoc = new Vector3(newStone.x + boardOffset.x + pieceOffset.x, 0, newStone.y + boardOffset.z + pieceOffset.z);
                        mouseOver = newStone;
                        SetStone(Stones.White, spawnLoc);

                        ResetPlayedStones();
                        ResetRemovedStones();
                    }
                    turnManager.SwitchTurn();
                }
            }

            if (Input.GetMouseButtonDown(0) && !mouseOver.Equals(notPos))
            {
                PlayStone(Stones.Black);
            }
            else if (Input.GetMouseButtonDown(1) && !mouseOver.Equals(notPos))
            {
                // Remove/pick up stone
                RemoveStone();
            }
            else if (Input.GetKeyDown(KeyCode.P))
            {
                Pass();
            }
        }
    }

    private bool isGameOver()
    {
        if (passCount > 1) return true;
        return false;
    }

    public void ResetPlayedStones()
    {
        playedStones.Clear();
    }

    public void ResetRemovedStones()
    {
        removedStones.Clear();
    }

    // runs whenever you right click a stone
    // Player must remove stones if they put too many on their turn
    // Player must also remove grampa's stones that they capture
    // If the player removes a grampa stone they shouldn't have, grampa will call them out and put it back down
    // Should probably refactor code to have basic Go stuff stay on the board script and have a player script which asks for certain info
    // Ex: for player playing regularly it just tells the board where it wants to place a stone and then the board will return if they did/can or not
    // Ex: for player removing captured stones they can ask for the block of white stones they have to remove, then if the player
    // removes any stones that aren't in that then grampa will tell them off (for cheating), small chance he misses you cheating
    // game continues as normal when you're finished removing the stones
    private void RemoveStone()
    {
        turnTimer.ResetTimer();
        if (turnTimer.paused)
        {
            turnTimer.StartTimer();
        }

        RaycastHit hit;
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, 25.0f, LayerMask.GetMask("Stone")))
        {
            Destroy(hit.transform.gameObject);
            grid[(int)mouseOver.x, (int)mouseOver.y] = (int)Stones.Empty;

            if (playedStones.Contains(mouseOver)) 
                playedStones.Remove(mouseOver);

            removedStones.Add(mouseOver);
        }
    }

    private void PlayStone(Stones stone = Stones.NULL)
    {
        if(stone == Stones.NULL)
        {
            if (turnManager.isBlackTurn()) stone = Stones.Black;
            else stone = Stones.White;
        }

        if (isPiece(mouseOver)) return;
        if(checkSuicideMove(mouseOver))
        {
            Debug.Log("Illegal Move: Suicide Move");
            return;
        }
        if (checkKo(mouseOver))
        {
            Debug.Log("Illegal Move: Ko");
            return;
        }

        turnTimer.ResetTimer();
        if (turnTimer.paused)
        {
            turnTimer.StartTimer();
        }

        Vector3 spawnLoc = new Vector3(mouseOver.x + boardOffset.x + pieceOffset.x, 0, mouseOver.y + boardOffset.z + pieceOffset.z);

        //if (turnManager.isBlackTurn()) SetStone(Stones.Black, spawnLoc);
        //else SetStone(Stones.White, spawnLoc);
        SetStone(stone, spawnLoc);

        playedStones.Add(mouseOver);

        passCount = 0;
    }

    // POSSIBLE ERROR: with cheating possible, we should make it so we do 2 Captures, one for each colour, in the case of misplay.
    // captures that should have happened through "cheating suicide" can occur and should be watched out for
    private void SetStone(Stones color, Vector3 spawnLoc)
    {
        GameObject stone;
        Quaternion rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        if(color == Stones.Black) stone = Instantiate(blackStone, spawnLoc, rotation);
        else stone = Instantiate(whiteStone, spawnLoc, rotation);

        grid[(int)mouseOver.x, (int)mouseOver.y] = (int)color;
        stones[(int)mouseOver.x, (int)mouseOver.y] = stone;
        Captures(3 - color);

        //save move if not part of a block to stop ko
        Count(mouseOver, color);
        if (block.Count == 1) prevMove[(int)color-1] = mouseOver;
        else prevMove[(int)color-1] = new Vector2(-1, -1);
        RestoreBoard();
    }

    private bool Pass()
    {
        passCount++;
        turnManager.SwitchTurn();
        turnTimer.PauseTimer();
        turnTimer.ResetTimer();
        //blackTurn = !blackTurn;
        // is the game over?
        Debug.Log("Turn passed");
        if (passCount > 1)
        {
            Debug.Log("The game is over!");
            ghostStoneInstance.transform.position += ghostStoneInstance.transform.up * 100f;
            PointTotals();
            return true;
        }
        return false;
    }

    private bool isPiece(Vector2 pos)
    {
        if (pos.x == -1 || pos.y == -1) return false;

        int gridpos = grid[(int)pos.x, (int)pos.y];

        int check = (gridpos & 3);

        if (check > 0) return true;

        return false;
    }

    private void UpdateMouseOver()
    {
        if (!Camera.main)
        {
            Debug.Log("Unable to find main camera");
            return;
        }

        // hide ghost stone if there's already a piece there
        RaycastHit hit;
        if(Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, 25.0f, LayerMask.GetMask("Board")))
        {
            mouseOver.x = (int)(hit.point.x - boardOffset.x);
            mouseOver.y = (int)(hit.point.z - boardOffset.z);

            if (!isPiece(mouseOver))
            {
                Vector3 boardLoc = new Vector3(mouseOver.x + boardOffset.x + pieceOffset.x, 0, mouseOver.y + boardOffset.z + pieceOffset.z);
                // probably will have to make it so mouseOver isn't actually updated so that when you left click stuff will always
                // be placed at the ghost location
                ghostStoneInstance.transform.position = boardLoc;
            }
        }
        else
        {
            mouseOver.x = -1;
            mouseOver.y = -1;
            ghostStoneInstance.transform.position += ghostStoneInstance.transform.up * 100f;
        }
    }

    private bool checkKo(Vector2 pos)
    {
        //if (blackTurn && prevMove[0].Equals(notPos))
        if (turnManager.isBlackTurn() && prevMove[0].Equals(notPos))
            return false;
        //else if(!blackTurn && prevMove[1].Equals(notPos))
        else if (turnManager.isWhiteTurn() && prevMove[1].Equals(notPos))
                    return false;

        // Does move put you in the same previous position?
        //if (blackTurn && prevMove[0].Equals(pos)) return true;
        //if (!blackTurn && prevMove[1].Equals(pos)) return true;
        if (turnManager.isBlackTurn() && prevMove[0].Equals(pos)) return true;
        if (turnManager.isWhiteTurn() && prevMove[1].Equals(pos)) return true;

        return false;
    }

    // player should be allowed to place their piece if it kills a group (removes all of their liberties)
    // in this case the piece won't die, but the group will
    private bool checkSuicideMove(Vector2 pos)
    {
        // suicide move
        //make move
        //grid[(int)pos.x, (int)pos.y] = (blackTurn ? (int)Stones.Black : (int)Stones.White);
        grid[(int)pos.x, (int)pos.y] = (turnManager.isBlackTurn() ? (int)Stones.Black : (int)Stones.White);
        //count liberties
        //Count(pos, (blackTurn ? Stones.Black : Stones.White));
        Count(pos, (turnManager.isBlackTurn() ? Stones.Black : Stones.White));
        //if liberties==0
        if (liberties.Count == 0)
        {
            //restore board
            RestoreBoard();

            // Check if any of the pieces surrounding would be captured instead
            // If it's the case that the other pieces would be captured, this stone would no longer be considered a suicide move
            //Count(new Vector2(pos.x, pos.y+1), (blackTurn ? Stones.White : Stones.Black));
            Count(new Vector2(pos.x, pos.y + 1), (turnManager.isBlackTurn() ? Stones.White : Stones.Black));
            if (liberties.Count == 0)
            {
                grid[(int)pos.x, (int)pos.y] = (int)Stones.Empty;
                return false;
            }
            RestoreBoard();
            //Count(new Vector2(pos.x+1, pos.y), (blackTurn ? Stones.White : Stones.Black));
            Count(new Vector2(pos.x + 1, pos.y), (turnManager.isBlackTurn() ? Stones.White : Stones.Black));
            if (liberties.Count == 0)
            {
                grid[(int)pos.x, (int)pos.y] = (int)Stones.Empty;
                return false;
            }
            RestoreBoard();
            //Count(new Vector2(pos.x, pos.y-1), (blackTurn ? Stones.White : Stones.Black));
            Count(new Vector2(pos.x, pos.y - 1), (turnManager.isBlackTurn() ? Stones.White : Stones.Black));
            if (liberties.Count == 0)
            {
                grid[(int)pos.x, (int)pos.y] = (int)Stones.Empty;
                return false;
            }
            RestoreBoard();
            //Count(new Vector2(pos.x-1, pos.y), (blackTurn ? Stones.White : Stones.Black));
            Count(new Vector2(pos.x - 1, pos.y), (turnManager.isBlackTurn() ? Stones.White : Stones.Black));
            if (liberties.Count == 0)
            {
                grid[(int)pos.x, (int)pos.y] = (int)Stones.Empty;
                return false;
            }
            RestoreBoard();

            // Suicide move confirmed
            //take off the stone
            grid[(int)pos.x, (int)pos.y] = (int)Stones.Empty;

            return true;
        }
        else
        {
            //restore board
            RestoreBoard();
            //take off the stone
            grid[(int)mouseOver.x, (int)mouseOver.y] = (int)Stones.Empty;

            return false;
        }
    }

    //remove captured stones
    private void ClearBlock()
    {
        foreach (Vector2 captured in block)
        {
            grid[(int)captured.x, (int)captured.y] = (int)Stones.Empty;

            if (stones[(int)captured.x, (int)captured.y] == null) return;

            Destroy(stones[(int)captured.x, (int)captured.y]);
            stones[(int)captured.x, (int)captured.y] = null;
        }
    }

    //restore the board after counting stones
    private void RestoreBoard()
    {
        //clear block and liberties list
        block.Clear();
        liberties.Clear();

        //unmark stones
        for (int i = 0; i < boardSize; i++)
        {
            for (int j = 0; j < boardSize; j++)
            {
                //restore piece
                grid[j, i] &= 51; //removes anything that isn't black or white (0011) OR a black or white point (110000)
            }
        }
    }

    // counting liberties and blocks of stones
    List<Vector2> block = new List<Vector2>();
    List<Vector2> liberties = new List<Vector2>();
    private void Count(Vector2 pos, Stones color)
    {
        // skip offboard squares
        if (pos.x == -1 || pos.y == -1) return;
        if (pos.x >= boardSize || pos.y >= boardSize) return;

        int gridpos = grid[(int)pos.x, (int)pos.y];

        //if there's a stone at square
        //and it is the given colour
        //and it is not marked
        int isColor = gridpos & (int)color;
        int isMarker = gridpos & (int)Stones.Marker;
        if (isPiece(pos) && isColor > 0 && isMarker == 0)
        {
            // save stone's coordinate
            block.Add(pos);

            // mark the stone
            grid[(int)pos.x, (int)pos.y] |= (int)Stones.Marker;

            // NORTH
            Count(new Vector2(pos.x, pos.y + 1f), color);
            // EAST
            Count(new Vector2(pos.x + 1f, pos.y), color);
            // SOUTH
            Count(new Vector2(pos.x, pos.y - 1f), color);
            // WEST
            Count(new Vector2(pos.x - 1f, pos.y), color);
        }
        // else if there is no stone at x
        else if (!isPiece(pos))
        {
            // mark the liberty
            grid[(int)pos.x, (int)pos.y] |= (int)Stones.Liberty;

            // save liberties
            liberties.Add(pos);
        }
    }

    //handle captures
    private void Captures(Stones color)
    {
        //loop over the board squares
        for (int i = 0; i < boardSize; i++)
        {
            for (int j = 0; j < boardSize; j++)
            {
                //init piece
                int gridpos = grid[j, i];

                // if stone belongs to the given color
                int isColor = gridpos & (int)color;
                if(isColor > 0)
                {
                    // count liberties
                    Count(new Vector2((float)j, (float)i), color);

                    // if no liberties left, remove the stones
                    if (liberties.Count == 0) ClearBlock();

                    // restore the board
                    RestoreBoard();
                }
            }
        }
    }

    //count points recursively. From a position look everywhere in the territory
    //if you come across a square with a stone, note what stone you saw and return to previous square to continue.
    //if you come across just one type of stone, the point goes to that colour
    //if you come across both types of stone, the point goes to no one.
    //(like Count(), but for empty positions in a way)
    //(a colour "capturing" empty squares basically, that's how it works
    private void PointTotals()
    {
        int blackPoints = 0;
        int whitePoints = 0;

        RestoreBoard();
        MarkAllPoints();

        for (int i = 0; i < boardSize; i++)
        {
            for (int j = 0; j < boardSize; j++)
            {
                if ((grid[j, i] & 48) > 0)
                {
                    if (((grid[i, j] & (int)Stones.BlackPoint) > 0) && ((grid[i, j] & (int)Stones.WhitePoint) > 0)) continue;
                    else if ((grid[i, j] & (int)Stones.BlackPoint) > 0)
                        blackPoints++;
                    else if ((grid[i, j] & (int)Stones.WhitePoint) > 0)
                        whitePoints++;
                }
            }
        }

        Debug.Log(blackPoints);
        Debug.Log(whitePoints);
    }

    // MISSING THE FACT THAT YOU HAVE TO REMOVE PRISONERS!!!
    // OR MAKE THE GAME USE JAPANESE POINT COUNTING RULES (although I really want to have this version work)
    private void MarkAllPoints()
    {
        for (int i = 0; i < boardSize; i++)
        {
            for (int j = 0;j < boardSize; j++)
            {
                if ((grid[j, i] & 48) > 0) continue;

                MarkPoint(new Vector2(j, i));
                foreach(Vector2 pos in block)
                {
                    if (hasBlack) grid[(int)pos.x, (int)pos.y] |= (int)Stones.BlackPoint;
                    if (hasWhite) grid[(int)pos.x, (int)pos.y] |= (int)Stones.WhitePoint;
                }
                hasBlack = false;
                hasWhite = false;
                RestoreBoard();
            }
        }
    }

    bool hasBlack = false;
    bool hasWhite = false;
    private void MarkPoint(Vector2 pos)
    {
        //find if a position should be marked as a point for either black, white, or neutral

        // skip offboard squares
        if (pos.x == -1 || pos.y == -1) return;
        if (pos.x >= boardSize || pos.y >= boardSize) return;

        int gridpos = grid[(int)pos.x, (int)pos.y];

        //If there is no piece and it hasn't been marked as a point
        int isMarker = gridpos & (int)Stones.Marker;
        if (!isPiece(pos) && isMarker == 0)
        {
            // save empty spot's coordinate
            block.Add(pos);

            // mark the position (so we don't go back to it accidentally)
            grid[(int)pos.x, (int)pos.y] |= (int)Stones.Marker;

            // NORTH
            MarkPoint(new Vector2(pos.x, pos.y + 1f));
            // EAST
            MarkPoint(new Vector2(pos.x + 1f, pos.y));
            // SOUTH
            MarkPoint(new Vector2(pos.x, pos.y - 1f));
            // WEST
            MarkPoint(new Vector2(pos.x - 1f, pos.y));
        }
        // else if there is a stone at position
        else if (isPiece(pos))
        {
            //mark entire block of this recursive statement as being worth a point of that colour
            //if the position has both colour, it's a neutral point
            if ((grid[(int)pos.x, (int)pos.y] & (int)Stones.Black) > 0) hasBlack = true;
            else hasWhite = true;
        }
    }
}
