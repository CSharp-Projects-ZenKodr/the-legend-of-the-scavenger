﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic; //Allows us to use Lists. 

using UnityEngine.UI;             //Allows us to use UI.
using UnityEngine.SceneManagement;
using UnityEngine.Advertisements;

//For serialization, save & load
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

//Class used to be saved to a file.
[Serializable]
class PlayerData
{
    public bool gameSaved; // true if it there is a game saved
    public int playerFoodPoints;
    public int level;
    public int maxLevel;   
}

public class GameManager : MonoBehaviour
{
	public float levelStartDelay = 2f;                      //Time to wait before starting level, in seconds.
	public float turnDelay = 0.1f;                          //Delay between each Player turn.
	public int playerFoodPoints = 100;                      //Starting value for Player food points.
	public static GameManager instance = null;              //Static instance of GameManager which allows it to be accessed by any other script.
	[HideInInspector]
	public bool playersTurn = true;                         //Boolean to check if it's players turn, hidden in inspector but public.		

    private Player player;                                  //Player object. Used to get and set food points.
    private Text levelText;                                 //Text to display current level number.
	private Text maxDaysText;                               //Text to display the maximum level (or days) reached.
	private GameObject levelImage;                          //Image to block out level as levels are being set up, background for levelText.
	private GameObject storyImage;							//Image to show the story at the beginning of the game.
	private GameObject exitImage;							//Image to show the continue/exit game options.
	private BoardManager boardScript;                       //Store a reference to our BoardManager which will set up the level.
	private int level = 0;                                  //Current level number, expressed in game as "Day 1".
	private int maxLevel = 1;                               //Maximum level reached
	private List<Enemy> enemies;                            //List of all Enemy units, used to issue them move commands.
	private bool enemiesMoving;                             //Boolean to check if enemies are moving.
	private bool doingSetup = true;                         //Boolean to check if we're setting up board, prevent Player from moving during setup.
	private bool onPause = false;                           //Boolean to check if the game was paused.    
    private string savedFilePath;                           //Filename where the game will be saved
		
	//Awake is always called before any Start functions
	void Awake ()
	{
		//Check if instance already exists
		if (instance == null) {
				
			//if not, set instance to this
			instance = this;
			
		//If instance already exists and it's not this:
		} else if (instance != this) {
				
			//Then destroy this. This enforces our singleton pattern, meaning there can only ever be one instance of a GameManager.
			Destroy (gameObject);   
		}
			
		//Sets this to not be destroyed when reloading scene
		DontDestroyOnLoad (gameObject);
			
		//Assign enemies to a new List of Enemy objects.
		enemies = new List<Enemy> ();

        //Get the player reference
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<Player>();

        //Get a component reference to the attached BoardManager script
        boardScript = GetComponent<BoardManager> ();

        savedFilePath = Application.persistentDataPath + "/playerInfo.dat";
    }

    void ShowAd()
    {
        #if UNITY_IOS || UNITY_ANDROID || UNITY_WP8 || UNITY_IPHONE

        if (Advertisement.IsReady())
        {
            Advertisement.Show();
        }

        #endif //End of mobile platform dependendent compilation section started above with #if
    }


	void OnEnable()
	{
		//Tell our 'OnLevelFinishedLoading' function to start listening for a scene change as soon as this script is enabled.
		SceneManager.sceneLoaded += OnLevelFinishedLoading;
	}

	void OnDisable()
	{
		
	}

    //Unity API. This is called each time a scene is loaded. 
	void OnLevelFinishedLoading (Scene scene, LoadSceneMode mode)
	{
        //Add one to our level number.
        level++;

		if (level > 1) {
			if (level % 5 == 0) {
				ShowAd ();
			}

			//Call InitGame to initialize our level.
			InitGame ();
		}
    }

    //Function called from Loader script to check if there is a game saved
    public bool GameSaved()
    {
        if (File.Exists(savedFilePath))
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(savedFilePath, FileMode.Open);
            PlayerData data = (PlayerData)bf.Deserialize(file);
            file.Close();

            return data.gameSaved;
        }

        return false;
    }

    //Function called from Loader script
    public void NewGame()
	{
        if (File.Exists(savedFilePath))
        {            
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(savedFilePath, FileMode.Open);
            PlayerData data = (PlayerData)bf.Deserialize(file);
            file.Close();

            //First: read maxLevel
            maxLevel = data.maxLevel;

            //Now: set gameSaved as false
            file = File.Open(savedFilePath, FileMode.Open, FileAccess.ReadWrite);            
            data = new PlayerData();            
            data.gameSaved = false;
            data.maxLevel = maxLevel;

            bf.Serialize(file, data);            

            file.Close();
        }

		InitGame ();
	}

    //Function called from Loader script
    public void LoadGame()
    {
        if (File.Exists(savedFilePath))
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(savedFilePath, FileMode.Open);
            PlayerData data = (PlayerData)bf.Deserialize(file);
            file.Close();

            playerFoodPoints = data.playerFoodPoints;
            level = data.level;
            maxLevel = data.maxLevel;
        } 
        
        player.UpdateFood(playerFoodPoints);

        InitGame();
    }

    //Function called from Loader script
    public void SaveGame()
    {   
        BinaryFormatter bf = new BinaryFormatter();
        //Unity has a persistent data path.
        //It's different depending on the platform.
        //I.e.: in Windows the path is AppData\Roaming...
        FileStream file = File.Create(savedFilePath);
        
        PlayerData data = new PlayerData();
        data.gameSaved = true;
        data.playerFoodPoints = player.Food();
        data.level = level;
        data.maxLevel = maxLevel;

        bf.Serialize(file, data);
        file.Close();
    }

    //Function called from Loader script
    public void ContinueGame()
	{
		//Hide ExitImage
		exitImage.SetActive (false);

		onPause = false;
	}
		
	//Initializes the game for each level.
	void InitGame ()
	{
		//While doingSetup is true the player can't move, prevent player from moving while title card is up.
		doingSetup = true;

		//Hide ExitImage
		exitImage = GameObject.Find ("ExitImage");
		exitImage.SetActive (false);

		//Hide the story text
		storyImage = GameObject.Find ("StoryImage");
		storyImage.SetActive (false);

		//Get a reference to our image LevelImage by finding it by name.
		levelImage = GameObject.Find ("LevelImage");
			
		//Get a reference to our text LevelText's text component by finding it by name and calling GetComponent.
		levelText = GameObject.Find ("LevelText").GetComponent<Text> ();
	
		//Set the text of levelText to the string "Day" and append the current level number.
		levelText.text = "Day " + level;

        //Get a reference to our text MaxDaysText's text component by finding it by name and calling GetComponent.
        maxDaysText = GameObject.Find("MaxDaysText").GetComponent<Text>();

        //Set the text of maxDaysText to the string "Max days survived: " and append maxLevel.
        if (maxLevel <= level)
        {
            maxLevel = level;
        }
        maxDaysText.text = "Max days survived: " + maxLevel;			
			
		//Set levelImage to active blocking player's view of the game board during setup.
		levelImage.SetActive (true);
			
		//Call the HideLevelImage function with a delay in seconds of levelStartDelay.
		Invoke ("HideLevelImage", levelStartDelay);
			
		//Clear any Enemy objects in our List to prepare for next level.
		enemies.Clear ();
			
		//Call the SetupScene function of the BoardManager script, pass it current level number.
		boardScript.SetupScene (level);			
	}
		
		
	//Hides black image used between levels
	void HideLevelImage ()
	{
		//Disable the levelImage gameObject.
		levelImage.SetActive (false);
			
		//Set doingSetup to false allowing player to move again.
		doingSetup = false;
	}
		
	//Update is called every frame.
	void Update ()
	{	
		// The "escape" key label = back button on the Android platform
		if (Input.GetKey ("escape") && onPause == false) {
			onPause = true;
			exitImage.SetActive (true);
		}

		//Check that playersTurn or enemiesMoving or doingSetup are not currently true.
		if (playersTurn || enemiesMoving || doingSetup || onPause) {				
			//If any of these are true, return and do not start MoveEnemies.
			return;
		}

		//Start moving enemies.
		StartCoroutine (MoveEnemies ());
	}
		
	//Call this to add the passed in Enemy to the List of Enemy objects.
	public void AddEnemyToList (Enemy script)
	{
		//Add Enemy to List enemies.
		enemies.Add (script);
	}
		
		
	//GameOver is called when the player reaches 0 food points
	public void GameOver ()
	{
		//Set levelText to display number of levels passed and game over message
		levelText.text = "After " + level + " days, \n you starved.";
			
		//Enable black background image gameObject.
		levelImage.SetActive (true);
			
		//Get the Canvas Animator and play the Restart clip.
		//The Loop Time for this clip was unchecked.
		Animator restart = GameObject.Find ("Canvas").GetComponent<Animator>();		
		restart.SetTrigger("GameOver");

		//Disable this GameManager.
		enabled = false;
	}

	//Restart is called when the user click the Restart button
	public void Restart ()
	{
		playerFoodPoints = 100;
		level = 0;
		SoundManager.instance.musicSource.Play ();

		//Enable this GameManager.
		enabled = true;

		SceneManager.LoadScene (SceneManager.GetActiveScene().name);
	}
		
	//Coroutine to move enemies in sequence.
	IEnumerator MoveEnemies ()
	{
		//While enemiesMoving is true player is unable to move.
		enemiesMoving = true;
			
		//Wait for turnDelay seconds, defaults to .1 (100 ms).
		yield return new WaitForSeconds (turnDelay);
			
		//If there are no enemies spawned (IE in first level):
		if (enemies.Count == 0) {
			//Wait for turnDelay seconds between moves, replaces delay caused by enemies moving when there are none.
			yield return new WaitForSeconds (turnDelay);
		}
			
		//Loop through List of Enemy objects.
		for (int i = 0; i < enemies.Count; i++) {
			//Call the MoveEnemy function of Enemy at index i in the enemies List.
			enemies [i].MoveEnemy ();
				
			//Wait for Enemy's moveTime before moving next Enemy, 
			yield return new WaitForSeconds (enemies [i].moveTime);
		}
		//Once Enemies are done moving, set playersTurn to true so player can move.
		playersTurn = true;
			
		//Enemies are done moving, set enemiesMoving to false.
		enemiesMoving = false;
	}
}
