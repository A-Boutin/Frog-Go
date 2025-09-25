using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    private bool blackTurn = true;

    public Board board;

    public bool getTurn()
    {
        return blackTurn;
    }

    public bool SwitchTurn()
    {
        blackTurn = !blackTurn;
        return blackTurn;
    }

    public bool isBlackTurn()
    {
        return blackTurn;
    }
    public bool isWhiteTurn()
    {
        return (!blackTurn);
    }
}
