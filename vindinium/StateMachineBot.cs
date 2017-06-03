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
	}

	class PathNode
	{
		public Pos pos = null;
		public double aStarF = 0;

		public PathNode (Pos posVal = null, double aStarF_Val = 0) {
			pos = posVal;
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
		private Pos currentTargetPos = null;
		private List<string> pathList = new List<string> ();

		public StateMachineBot(ServerStuff serverStuff)
        {
            this.serverStuff = serverStuff;
        }

        //starts everything
        public void run()
        {
			Console.Out.WriteLine("State Machine Bot running");

            serverStuff.createGame();

            if (serverStuff.errored == false) {
                //opens up a webpage so you can view the game, doing it async so we dont time out
                new Thread(delegate() {
                    System.Diagnostics.Process.Start(serverStuff.viewURL);
				}).Start();

				Console.Out.WriteLine ("serverStuff.maxTurns: " + serverStuff.maxTurns);
				Console.Out.WriteLine("my Hero (" + serverStuff.myHero.id + ") start position: " + correctedHeroPos ().x + ", " + correctedHeroPos ().y);
				foreach (Hero currHero in serverStuff.heroes) {
					Console.Out.WriteLine("Hero (" + currHero.id + ") start position: " + currHero.pos.x + ", " + currHero.pos.y);
				}

				// memorize key aspects of the map
				memorizeMap ();
            }

            while (serverStuff.finished == false && serverStuff.errored == false) {
				serverStuff.moveHero(determineMove ());

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
			// check my hero's health and if my hero is in first near the end of the game
			bool rank1NearGameEnd = serverStuff.board.Rank == 1 && myHero.gold > 0 && (float)serverStuff.currentTurn/(float)serverStuff.maxTurns <= 0.25f;
			if (myHero.life < 20 || rank1NearGameEnd) {
				// find a tavern
				Pos tavernPos = findClosestTavernPos ();
				if (currentTargetPos == null || !currentTargetPos.EqualsPos (tavernPos)) {
					currentTargetPos = tavernPos;
					// wipe our current math and re-determine it
					pathList.Clear ();

					pathList = bestPathToPos (currentTargetPos);
				}

				if (pathList.Count > 0) {
					returnVal = pathList [0];
					pathList.RemoveAt (0);
					return returnVal;
				}
			}

			// check how many moves are left

			// check nearby tiles
			Pos neutralMinePos = findClosestNeutralMinePos ();
			if (neutralMinePos != null) {
				if (currentTargetPos == null || !currentTargetPos.EqualsPos (neutralMinePos)) {
					currentTargetPos = neutralMinePos;
					// wipe our current math and re-determine it
					pathList.Clear ();

					pathList = bestPathToPos (currentTargetPos);
				}

				if (pathList.Count > 0) {
					returnVal = pathList [0];
					pathList.RemoveAt (0);
				}
			} else {
				Pos beatableHeroPos = findClosestBeatableHeroPos ();
				if (beatableHeroPos != null) {
					if (currentTargetPos == null || !currentTargetPos.EqualsPos (beatableHeroPos)) {
						currentTargetPos = beatableHeroPos;
						// wipe our current math and re-determine it
						pathList.Clear ();

						pathList = bestPathToPos (currentTargetPos);
					}

					if (pathList.Count > 0) {
						returnVal = pathList [0];
						pathList.RemoveAt (0);
					}
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
				Console.Out.WriteLine (" ----- bestPathToPos NOT FOUND!!!");
			} else {
				int i = 0;
				foreach (string currStr in bestPath) {
					Console.Out.WriteLine("bestPathToPos bestPath step" + i + ": " + currStr);
					++i;
				}
			}

			return bestPath;
		}

		private bool findBestPathFromPosToPos (Pos currentPos, Pos targetPos, out List<string> bestPath) {
			bestPath = new List<string> ();

			if (currentPos.EqualsPos (targetPos)) {
				bestPathFound = true;
				return true;
			}
			else if (bestPathFound)
				return false;

			List<PathNode> openList = new List<PathNode> ();
			Pos myHeroPos = correctedHeroPos ();
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
			if (currTile != Tile.IMPASSABLE_WOOD && currTile != Tile.OFF_MAP) {
				g = myHeroPos.Distance (adjPos);
				h = currentPos.Distance (targetPos);
				f = g + h;
				adjNode = new PathNode (adjPos, f);
				openList.Add (adjNode);
			}

			// east
			adjPos.x = currentPos.x + 1;
			adjPos.y = currentPos.y;
			currTile = tileForCoords (adjPos.x, adjPos.y);
			if (currTile != Tile.IMPASSABLE_WOOD && currTile != Tile.OFF_MAP) {
				g = myHeroPos.Distance (adjPos);
				h = currentPos.Distance (targetPos);
				f = g + h;
				adjNode = new PathNode (adjPos, f);
				openList.Add (adjNode);
			}

			// south
			adjPos.x = currentPos.x;
			adjPos.y = currentPos.y + 1;
			currTile = tileForCoords (adjPos.x, adjPos.y);
			if (currTile != Tile.IMPASSABLE_WOOD && currTile != Tile.OFF_MAP) {
				g = myHeroPos.Distance (adjPos);
				h = currentPos.Distance (targetPos);
				f = g + h;
				adjNode = new PathNode (adjPos, f);
				openList.Add (adjNode);
			}

			// west
			adjPos.x = currentPos.x - 1;
			adjPos.y = currentPos.y;
			currTile = tileForCoords (adjPos.x, adjPos.y);
			if (currTile != Tile.IMPASSABLE_WOOD && currTile != Tile.OFF_MAP) {
				g = myHeroPos.Distance (adjPos);
				h = currentPos.Distance (targetPos);
				f = g + h;
				adjNode = new PathNode (adjPos, f);
				openList.Add (adjNode);
			}

			// sort the list based on the f value
			openList.Sort (delegate (PathNode a, PathNode b) {
				return a.aStarF.CompareTo (b.aStarF);
			});

			double lowestF_Val = 99999;
			foreach (PathNode currNode in openList) {
				// make sure the newly added nodes are not in the closed list already
				bool skipCurrNode = false;
				foreach (PathNode currClosedNode in closedList) {
					if (currClosedNode.EqualsNode (currNode)) {
						skipCurrNode = true;
						break;
					}
				}

				if (skipCurrNode) {
					continue;
				}

				if (currNode.aStarF <= lowestF_Val) {
					lowestF_Val = currNode.aStarF;

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

					continue;
				}

				// if we're here we've passed the lowest f value nodes so we can stop
				break;
			}

			return false;
		}

		#endregion

		#region Helper Methods

		// finds the closest hero in relation to the bot (myHero)
		// -x value is to the left or west
		// -y value is above or to the north
		// IMPORTANT: null Pos means there were no beatable heroes found (in other words they all have greater health then myHero)
		private Pos findClosestBeatableHeroPos () {
			Hero myHero = serverStuff.myHero;
			Pos myHeroPos = correctedHeroPos ();
			Pos closestBeatableHeroPos = null;
			foreach (Hero currHero in serverStuff.heroes) {
				if (myHero.life > currHero.life) {
					if (closestBeatableHeroPos == null || myHeroPos.Distance (currHero.pos) < myHeroPos.Distance (closestBeatableHeroPos)) {
						closestBeatableHeroPos = currHero.pos;
					}
				}
			}

			return closestBeatableHeroPos;
		}

		// finds the closest mine in relation to the bot (myHero)
		// -x value is to the left or west
		// -y value is above or to the north
		// IMPORTANT: null Pos means there were no neutral mines found (in other words they're all controlled)
		private Pos findClosestNeutralMinePos () {
			Pos myHeroPos = correctedHeroPos ();
			Pos closestNeutralMinePos = null;
			foreach (Pos currMinePos in minesPosList) {
				if (tileForCoords (currMinePos.x, currMinePos.y) == Tile.GOLD_MINE_NEUTRAL) {
					if (closestNeutralMinePos == null || myHeroPos.Distance (currMinePos) < myHeroPos.Distance (closestNeutralMinePos)) {
						closestNeutralMinePos = currMinePos;
					}
				}
			}

			return closestNeutralMinePos;
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
