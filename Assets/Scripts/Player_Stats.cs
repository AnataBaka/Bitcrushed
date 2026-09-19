using UnityEngine;
[System.Serializable]
public class CharacterStats
{

    public int maxHealth =80;
    public int currHealth = 80;
    public int speed = Random.Range(1,8); //rogues get a buff
    public int mana = Random.Range(80,100); //mages get a flat 100
    public int dexterity = Random.Range(1,8); //archer gets a buff
    public int intellignece = Random.Range(1,2); //mages get a buff
    public int atk = Random.Range(5,10); // warriors get a buff
    public int xp = 0;
    


    
}
