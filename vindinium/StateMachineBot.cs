using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vindinium
{
	static class Extensions
	{
		public static double Distance (this Pos thisPos, Pos otherPos) {
			return Math.Sqrt (Math.Pow (thisPos.x - otherPos.x, 2) + Math.Pow (thisPos.y - otherPos.y, 2));
		}

		public static bool EqualsPos (this Pos thisPos, Pos otherPos) {
			if (otherPos != null && thisPos.x == otherPos.x && thisPos.y == otherPos.y) {
				return true;
			}

			return false;
		}

		public static Pos GetCorrectedHeroPos (this Hero hero) {
			Pos correctedPos = new Pos ();
			correctedPos.x = hero.pos.y;
			correctedPos.y = hero.pos.x;

			return correctedPos;
		}

		public static bool ContainsPathNode (this List<PathNode> nodeList, PathNode node) {
			foreach (PathNode currNode in nodeList) {
				if (currNode.EqualsNode (node)) {
					return true;
				}
			}

			return false;
		}
	}

	class PathNode
	{
		public Pos pos = new Pos ();
		public double aStarF = 0;

		public PathNode (Pos posVal = null, double aStarF_Val = 0) {
			pos.x = posVal.x;
			pos.y = posVal.y;
			aStarF = aStarF_Val;
		}

		public bool EqualsNode (PathNode otherNode) {
			if (pos != null) {
				return pos.EqualsPos (otherNode.pos);
			}

			return false;
		}
	}

    class StateMachineBot
    {
		private ServerStuff serverStuff = null;
		private List<Pos> minesPosList = new List<Pos> ();
		private List<Pos> tavernsPosList = new List<Pos> ();
		private MyHeroState currentState = MyHeroState.NONE;
		private Pos currentTargetPos = null;
		private List<string> pathList = new List<string> ();

		public StateMachineBot(ServerStuff serverStuff)
        {
            this.serverStuff = serverStuff;
        }

        //starts everything
        public void run()
        {
			//Console.Out.WriteLine("State Machine Bot running");

            serverStuff.createGame();

            if (serverStuff.errored == false) {
                //opens up a webpage so you can view the game, doing it async so we dont time out
                new Thread(delegate() {
                    System.Diagnostics.Process.Start(serverStuff.viewURL);
				}).Start();

				Console.Out.WriteLine ("serverStuff.maxTurns: " + serverStuff.maxTurns);
				Console.Out.WriteLine("my Hero (" + serverStuff.myHero.id + ") start position: " + correctedHeroPos ().x + ", " + correctedHeroPos ().y);
				foreach (Hero currHero in serverStuff.heroes) {
					Pos heroPos = currHero.GetCorrectedHeroPos ();
					Console.Out.WriteLine("Hero (" + currHero.id + ") start position: " + heroPos.x + ", " + heroPos.y);
				}

				// memorize key aspects of the map
				memorizeMap ();
            }

			while (serverStuff.finished == false && serverStuff.errored == false) {
				string chosenMove = determineMove ();
				Console.Out.WriteLine("chosenMove: " + chosenMove);
				serverStuff.moveHero(chosenMove);

                Console.Out.WriteLine("completed turn " + serverStuff.currentTurn);
            }

            if (serverStuff.errored) {
                Console.Out.WriteLine("error: " + serverStuff.errorText);
            }

            Console.Out.WriteLine("bot finished");
        }

		private string determineMove ()
		{
			string returnVal = Direction.Stay;

			Hero myHero = serverStuff.myHero;

			// check if we've gotten to the target (doesn't really happen unless the target pos is a free tile)
			if (currentTargetPos != null && myHero.GetCorrectedHeroPos ().EqualsPos (currentTargetPos)) {
				Console.Out.WriteLine ("found currentTargetPos: " + currentTargetPos.x + ", " + currentTargetPos.y);
				currentTargetPos = null;
			}
			// check if we just died
			if (serverStuff.currentTurn > 1 && myHero.life == 100 && myHero.pos.EqualsPos (myHero.spawnPos)) {
				// wipe our current path and reset some values, we'll re-evaluate below
				pathList.Clear ();
				currentTargetPos = null;
				currentState = MyHeroState.NONE;
				Console.Out.WriteLine ("reset, just died");
			}



			/****** TODO:
			 * check if higher rank player is closer than mine
			 * maybe check closest player near mine and health compared to my hero's health (are they near a tavern?)
			 * maybe check if you can swing by a tavern on the way to a particular target
			 * maybe check if we're being attacked, if so WHAT SHOULD WE DO?
			 */




			// check how many moves are left

			// check nearby tiles





			if (currentState != MyHeroState.CAPTURE_MINE) {
				//Console.Out.WriteLine ("findClosestMinePos");
				List <Pos> closestMinePosList = findClosestMinePositions (3);
				if (closestMinePosList.Count > 0) {
					//Console.Out.WriteLine ("findClosestMinePos count greater then 0");

					// find the best path for each mine so we can compare which one is truly closest
					int smallestPathLength = 99999;
					Pos closestMinePos = null;
					List<string> bestPathList = null;
					foreach (Pos currMinePos in closestMinePosList) {
						List<string> currBestPath = bestPathToPos (currMinePos);
						if (currBestPath.Count < smallestPathLength) {
							smallestPathLength = currBestPath.Count;
							bestPathList = currBestPath;
							closestMinePos = currMinePos;
						}
					}

					if (closestMinePos == null) {
						Console.Out.WriteLine ("findClosestMinePositions was null");
					} else if (currentTargetPos == null || !currentTargetPos.EqualsPos (closestMinePos)) {
						currentState = MyHeroState.CAPTURE_MINE;

						currentTargetPos = closestMinePos;
						Console.Out.WriteLine ("currentTargetPos: " + currentTargetPos.x + ", " + currentTargetPos.y);
						// wipe our current path and re-determine it
						pathList.Clear ();

						pathList = bestPathList;
					}
				}
			}

			if (currentState != MyHeroState.HEAL) {
				//Console.Out.WriteLine ("findClosestBeatableHeroPos");
				Pos beatableHeroPos = findClosestHeroOfInterestPos ();
				if (beatableHeroPos == null) {
					Console.Out.WriteLine ("findClosestBeatableHeroPos was null");
				} else {
					//Console.Out.WriteLine ("findClosestBeatableHeroPos not null");
					if (currentTargetPos == null || !currentTargetPos.EqualsPos (beatableHeroPos)) {
						currentState = MyHeroState.ATTACK;

						currentTargetPos = beatableHeroPos;
						Console.Out.WriteLine ("currentTargetPos: " + currentTargetPos.x + ", " + currentTargetPos.y);
						// wipe our current path and re-determine it
						pathList.Clear ();

						pathList = bestPathToPos (currentTargetPos);
					}
				}
			}

			// 1) Make sure we're not already headed for a tavern
			// 2) Check my hero's health, if the health minus the moves to the target is less than 20 we'll need to heal first
			// 3) Check if my hero is in first near the end of the game, if so we'll "suck our thumb"
			bool rank1NearGameEnd = serverStuff.board.Rank == 1 && myHero.gold > 0 && (float)serverStuff.currentTurn/(float)serverStuff.maxTurns >= 0.95f;
			if (myHero.life - pathList.Count <= 20 || rank1NearGameEnd) {
				Console.Out.WriteLine ("myHero.life: " + myHero.life);
				Console.Out.WriteLine ("rank1NearGameEnd: " + rank1NearGameEnd);
				// find a tavern
				Pos tavernPos = findClosestTavernPos ();
				if (tavernPos == null) {
					Console.Out.WriteLine ("findClosestTavernPos was null");
				} else if (currentTargetPos == null || !currentTargetPos.EqualsPos (tavernPos)) {
					currentState = MyHeroState.HEAL;
					currentTargetPos = tavernPos;
					Console.Out.WriteLine ("currentTargetPos: " + currentTargetPos.x + ", " + currentTargetPos.y);
					// wipe our current path and re-determine it
					pathList.Clear ();

					pathList = bestPathToPos (currentTargetPos);
				}
			}

			if (pathList.Count > 0) {
				// check if we're about to "enter" a tavern
				if (currentState == MyHeroState.HEAL && pathList.Count == 1) {
					// we're about to enter a tavern, let's see if we should heal up more than once
					// using 49 because the tavern will heal us by 50, but each turn we lose 1 health
					if (myHero.life + 49 < 90) {
						pathList.Add (pathList [0]);
					}
				}

				returnVal = pathList [0];
				pathList.RemoveAt (0);

				// check if we reached then end of our path and hopefully our target
				if (pathList.Count == 0) {
					currentState = MyHeroState.NONE;
					currentTargetPos = null;
				}
			}

			return returnVal;
		}

		/*private WeightedDecisions calculateDecision (int tileLimit = serverStuff.board.Length - 1)
		{
			Pos closestPubOffset = null;
			Pos closestNeutralMineOffset = null;

			for (int i = 1; i < tileLimit; ++i) {
				// northern tile
				Tile currTile = tileForOffset (0,-i);
				// eastern tile
				currTile = tileForOffset (i,0);
				// southern tile
				currTile = tileForOffset (0,i);
				// western tile
				currTile = tileForOffset (-i,0);

				for (int j = 1; j <= i; ++j) {
					// north easterly tile
					currTile = tileForOffset (j,-i);
					// south easterly tile
					currTile = tileForOffset (j,-i);
					// south westerly tile
					currTile = tileForOffset (-j,i);
					// north westerly tile
					currTile = tileForOffset (-j,i);

					if (j != i) {
						// north easterly tile
						currTile = tileForOffset (i,-j);
						// south easterly tile
						currTile = tileForOffset (i,-j);
						// south westerly tile
						currTile = tileForOffset (-i,j);
						// north westerly tile
						currTile = tileForOffset (-i,j);
					}
				}
			}
		}*/

		private void memorizeMap ()
		{
			// Generated maps are symmetric, and always contain 4 taverns and 4 heroes.
			// Therefore we only need to evaluate 1 quarter of the map
			for (int i = 0; i < (serverStuff.board.Length / 2); ++i) {
				for (int j = 0; j < (serverStuff.board[0].Length / 2); ++j) {
					Tile currTile = serverStuff.board [i] [j];

					Pos currQuadPos = new Pos ();
					currQuadPos.x = i;
					currQuadPos.y = j;

					// mirror to the right
					Pos rightQuadPos = new Pos ();
					rightQuadPos.x = serverStuff.board.Length - 1 - i;
					rightQuadPos.y = j;

					// mirror below
					Pos belowQuadPos = new Pos ();
					belowQuadPos.x = i;
					belowQuadPos.y = serverStuff.board[0].Length - 1 - j;

					// mirror below and to the right
					Pos belowAndRightQuadPos = new Pos ();
					belowAndRightQuadPos.x = rightQuadPos.x;
					belowAndRightQuadPos.y = belowQuadPos.y;

					switch (currTile) {
					case Tile.GOLD_MINE_NEUTRAL:
						minesPosList.Add (currQuadPos);
						// quadrant to right
						minesPosList.Add (rightQuadPos);
						// quadrant below
						minesPosList.Add (belowQuadPos);
						// quadrant below and to the right
						minesPosList.Add (belowAndRightQuadPos);
						break;
					case Tile.TAVERN:
						tavernsPosList.Add (currQuadPos);
						// quadrant to right
						tavernsPosList.Add (rightQuadPos);
						// quadrant below
						tavernsPosList.Add (belowQuadPos);
						// quadrant below and to the right
						tavernsPosList.Add (belowAndRightQuadPos);
						break;
					default:
						break;
					}
				}
			}

			Console.Out.WriteLine("memorized map =>");
			foreach (Pos currPos in minesPosList) {
				Console.Out.WriteLine ("mine: " + currPos.x + ", " + currPos.y);
			}
			foreach (Pos currPos in tavernsPosList) {
				Console.Out.WriteLine ("tavern: " + currPos.x + ", " + currPos.y);
			}
		}

		// TEMP, hero position seems to be backwards
		private Pos correctedHeroPos () {
			Pos correctedPos = new Pos ();
			correctedPos.x = serverStuff.myHero.pos.y;
			correctedPos.y = serverStuff.myHero.pos.x;

			return correctedPos;
		}

		#region A Star

		private bool bestPathFound = false;
		private List<PathNode> closedList = new List<PathNode> ();
		private List<string> bestPathToPos (Pos targetPos) {
			bestPathFound = false;
			closedList.Clear ();

			Pos myHeroPos = correctedHeroPos ();
			Console.Out.WriteLine("bestPathToPos bot start position: " + myHeroPos.x + ", " + myHeroPos.y);
			Console.Out.WriteLine("bestPathToPos target position: " + targetPos.x + ", " + targetPos.y);

			List<string> bestPath = new List<string> ();
			if (!findBestPathFromPosToPos (myHeroPos, targetPos, out bestPath)) {
				currentTargetPos = null;
				Console.Out.WriteLine (" ----- bestPathToPos NOT FOUND!!!");
			} /*else {
				int i = 0;
				foreach (string currStr in bestPath) {
					Console.Out.WriteLine("bestPathToPos bestPath step" + i + ": " + currStr);
					++i;
				}
			}*/

			return bestPath;
		}

		private bool findBestPathFromPosToPos (Pos currentPos, Pos targetPos, out List<string> bestPath) {
			bestPath = new List<string> ();

			//Console.Out.WriteLine("findBestPathFromPosToPos=> currentPos: " + currentPos.x + ", " + currentPos.y);

			if (currentPos.EqualsPos (targetPos)) {
				bestPathFound = true;
				return true;
			} else if (bestPathFound)
				return false;

			List<PathNode> openList = new List<PathNode> ();
			Pos adjPos = null;
			double g = 0;
			double h = 0;
			double f = g + h;
			PathNode adjNode = null;

			// only using direcations that we can validly move to 

			// north
			adjPos = new Pos ();
			adjPos.x = currentPos.x;
			adjPos.y = currentPos.y - 1;
			Tile currTile = tileForCoords (adjPos.x, adjPos.y);
			g = currentPos.Distance (adjPos);
			h = adjPos.Distance (targetPos);
			f = g + h;
			adjNode = new PathNode (adjPos, f);
			if (currTile == Tile.FREE || currTile == Tile.HERO_1 || currTile == Tile.HERO_2 || currTile == Tile.HERO_3 ||
				currTile == Tile.HERO_4 || adjPos.EqualsPos (targetPos)) {
				openList.Add (adjNode);
			} else if (!closedList.ContainsPathNode (adjNode)) {
				closedList.Add (adjNode);
			}

			// east
			adjPos.x = currentPos.x + 1;
			adjPos.y = currentPos.y;
			currTile = tileForCoords (adjPos.x, adjPos.y);
			g = currentPos.Distance (adjPos);
			h = adjPos.Distance (targetPos);
			f = g + h;
			adjNode = new PathNode (adjPos, f);
			if (currTile == Tile.FREE || currTile == Tile.HERO_1 || currTile == Tile.HERO_2 || currTile == Tile.HERO_3 ||
				currTile == Tile.HERO_4 || adjPos.EqualsPos (targetPos)) {
				openList.Add (adjNode);
			} else if (!closedList.ContainsPathNode (adjNode)) {
				closedList.Add (adjNode);
			}

			// south
			adjPos.x = currentPos.x;
			adjPos.y = currentPos.y + 1;
			currTile = tileForCoords (adjPos.x, adjPos.y);
			g = currentPos.Distance (adjPos);
			h = adjPos.Distance (targetPos);
			f = g + h;
			adjNode = new PathNode (adjPos, f);
			if (currTile == Tile.FREE || currTile == Tile.HERO_1 || currTile == Tile.HERO_2 || currTile == Tile.HERO_3 ||
				currTile == Tile.HERO_4 || adjPos.EqualsPos (targetPos)) {
				openList.Add (adjNode);
			} else if (!closedList.ContainsPathNode (adjNode)) {
				closedList.Add (adjNode);
			}

			// west
			adjPos.x = currentPos.x - 1;
			adjPos.y = currentPos.y;
			currTile = tileForCoords (adjPos.x, adjPos.y);
			g = currentPos.Distance (adjPos);
			h = adjPos.Distance (targetPos);
			f = g + h;
			adjNode = new PathNode (adjPos, f);
			if (currTile == Tile.FREE || currTile == Tile.HERO_1 || currTile == Tile.HERO_2 || currTile == Tile.HERO_3 ||
				currTile == Tile.HERO_4 || adjPos.EqualsPos (targetPos)) {
				openList.Add (adjNode);
			} else if (!closedList.ContainsPathNode (adjNode)) {
				closedList.Add (adjNode);
			}

			// sort the list based on the f value
			openList.Sort (delegate (PathNode a, PathNode b) {
				return a.aStarF.CompareTo (b.aStarF);
			});

			foreach (PathNode currNode in openList) {
				// make sure the newly added nodes are not in the closed list already
				if (closedList.ContainsPathNode (currNode)) {
					continue; // we've found a node that was already closed
				}

				closedList.Add (currNode);
				if (findBestPathFromPosToPos (currNode.pos, targetPos, out bestPath)) {
					if (currNode.pos.x > currentPos.x) {
						bestPath.Insert (0, Direction.East);
					} else if (currNode.pos.x < currentPos.x) {
						bestPath.Insert (0, Direction.West);
					} else if (currNode.pos.y < currentPos.y) {
						bestPath.Insert (0, Direction.North);
					} else if (currNode.pos.y > currentPos.y) {
						bestPath.Insert (0, Direction.South);
					}

					return true;
				}
			}

			return false;
		}

		#endregion

		#region Helper Methods

		// finds the closest hero in relation to the bot (myHero)
		// -x value is to the left or west
		// -y value is above or to the north
		// IMPORTANT: null Pos means there were no beatable heroes found (in other words they all have greater health then myHero)
		private Pos findClosestHeroOfInterestPos () {
			Hero myHero = serverStuff.myHero;
			Pos myHeroPos = correctedHeroPos ();
			Pos closestBeatableHeroPos = null;
			foreach (Hero currHero in serverStuff.heroes) {
				if (currHero.id != myHero.id) {
					bool otherHeroHasTooMuchMoreGold = currHero.gold - myHero.gold >= 250;
					bool otherHeroHasLessHealth = myHero.life > currHero.life;
					/* TODO:
					 * check if other hero has a decent amount more gold diff:
					 * -- check if they have more health and heal up before attacking
					 * */
					if (closestBeatableHeroPos == null || myHeroPos.Distance (currHero.GetCorrectedHeroPos ()) < myHeroPos.Distance (closestBeatableHeroPos)) {
						closestBeatableHeroPos = currHero.GetCorrectedHeroPos ();
					}
				}
			}

			return closestBeatableHeroPos;
		}

		// finds the closest mine in relation to the bot (myHero)
		// -x value is to the left or west
		// -y value is above or to the north
		// IMPORTANT: null Pos means there were no neutral mines found (in other words they're all controlled)
		private List<Pos> findClosestMinePositions (int amountToFind = 1) {
			List <Pos> returnList = new List<Pos> ();

			Tile heroMineTile = Tile.GOLD_MINE_1;
			switch (serverStuff.myHero.id) {
			case 2:
				heroMineTile = Tile.GOLD_MINE_2;
				break;
			case 3:
				heroMineTile = Tile.GOLD_MINE_3;
				break;
			case 4:
				heroMineTile = Tile.GOLD_MINE_4;
				break;
			default:
				break;
			}

			Pos myHeroPos = correctedHeroPos ();

			// sort the list of the mines in relation to the current hero's pos
			List <Pos> sortedList = new List<Pos> (minesPosList);
			sortedList.Sort (delegate (Pos a, Pos b) {
				return myHeroPos.Distance(a).CompareTo (myHeroPos.Distance(b));
			});

			if (amountToFind > sortedList.Count) {
				amountToFind = sortedList.Count;
			}
			for (int i = 0; i < amountToFind; ++i) {
				Pos currMinePos = sortedList[i];
				if (tileForCoords (currMinePos.x, currMinePos.y) != heroMineTile) {
					returnList.Add (currMinePos);
				}
			}

			/*Pos closestValidMinePos = null;
			foreach (Pos currMinePos in minesPosList) {
				// make sure it's not already controlled by us
				if (tileForCoords (currMinePos.x, currMinePos.y) != heroMineTile) {
					if (closestValidMinePos == null || myHeroPos.Distance (currMinePos) < myHeroPos.Distance (closestValidMinePos)) {
						closestValidMinePos = currMinePos;
					}
				}
			}*/

			return returnList;
		}

		// finds the closest tavern in relation to the bot (myHero)
		// -x value is to the left or west
		// -y value is above or to the north
		// IMPORTANT: should never be the case but null Pos means there were no taverns found (in other words this map doesn't have any taverns)
		private Pos findClosestTavernPos () {
			Pos myHeroPos = correctedHeroPos ();
			Pos closestTavernPos = null;
			foreach (Pos currTavernPos in tavernsPosList) {
				if (closestTavernPos == null || myHeroPos.Distance (currTavernPos) < myHeroPos.Distance (closestTavernPos)) {
					closestTavernPos = currTavernPos;
				}
			}

			return closestTavernPos;
		}

		// determines the offset from the bot (myHero) to another tile position
		// -x value is to the left or west
		// -y value is above or to the north
		private Pos offsetToTilePos (Pos tilePos) {
			Pos myHeroPos = correctedHeroPos ();
			Pos distPos = new Pos ();
			distPos.x = tilePos.x - myHeroPos.x;
			distPos.y = tilePos.y - myHeroPos.y;

			return distPos;
		}

		// determines the tile for an offset from the bot's (myHero) position
		private Tile tileForOffset (int x, int y)
		{
			x += correctedHeroPos ().x;
			y += correctedHeroPos ().y;

			return tileForCoords (x, y);
		}

		// determines the tile for specific coordinates
		private Tile tileForCoords (int x, int y)
		{
			// make sure we're not outside the map
			if (x >= serverStuff.board.Length) {
				//x = serverStuff.board.Length - 1;
				return Tile.OFF_MAP;
			} else if (x < 0) {
				//x = 0;
				return Tile.OFF_MAP;
			}
			if (y >= serverStuff.board [x].Length) {
				//y = serverStuff.board [x].Length - 1;
				return Tile.OFF_MAP;
			} else if (y < 0) {
				//y = 0;
				return Tile.OFF_MAP;
			}

			return serverStuff.board [x] [y];
		}

		#endregion
    }
}
