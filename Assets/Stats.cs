using UnityEngine;
[System.Serializable]
public class Characters
{
    public int health;
    public int speed;
    public int mana;
    public int dexterity;
    public int intelligence;

    public int health =80;
    public int speed = rand.Next(1,8); //rogues get a buff
    public int mana = rand.next(80,100); //mages get a flat 100
    public int dexterity = rand.next(1,8); //archer gets a buff
    public int intellignece = rand.next(1,2); //mages get a buff


    
}
