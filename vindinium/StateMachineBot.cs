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

		public static double EqualsPos (this Pos thisPos, Pos otherPos) {
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
			// check my health
			if (myHero.life < 20 || serverStuff.board.Rank == 1) {
				// find a tavern
				Pos tavernPos = findClosestTavernPos ();
				if (currentTargetPos == null || !currentTargetPos.EqualsPos (tavernPos)) {
					currentTargetPos = tavernPos;
					// wipe our current math and re-determine it
					pathList.Clear ();
				}

				returnVal = pathList [0];
				pathList.RemoveAt (0);
				return returnVal;
			}

			// check how many moves are left

			// check nearby tiles
		}

		private WeightedDecisions calculateDecision (int tileLimit = serverStuff.board.Length - 1)
		{
			Pos closestPubOffset = null;
			Pos closestNeutralMineOffset = null;

			for (int i = 1; i < tileLimit; ++i) {
				// northern tile
				Tile currTile = tileForOffset (0,i);
				// eastern tile
				currTile = tileForOffset (i,0);
				// southern tile
				currTile = tileForOffset (0,-i);
				// western tile
				currTile = tileForOffset (-i,0);

				for (int j = 1; j <= i; ++j) {
					// north easterly tile
					currTile = tileForOffset (j,i);
					// south easterly tile
					currTile = tileForOffset (j,-i);
					// south westerly tile
					currTile = tileForOffset (-j,-i);
					// north westerly tile
					currTile = tileForOffset (-j,i);

					if (j != i) {
						// north easterly tile
						currTile = tileForOffset (i,j);
						// south easterly tile
						currTile = tileForOffset (i,-j);
						// south westerly tile
						currTile = tileForOffset (-i,-j);
						// north westerly tile
						currTile = tileForOffset (-i,j);
					}
				}
			}
		}

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
		}

		#region A Star

		private List<string> bestPathToPos (Pos targetPos) {
			
		}

		private List<PathNode> closedList = new List<Pos> ();
		private bool findBestPathFromPosToPos (Pos currentPos, Pos targetPos) {
			List<PathNode> openList = new List<Pos> ();

			Pos adjPos = new Pos ();
			adjPos = currentPos.y + 1;
			double g = 
			double f = 
			PathNode adjNode = new PathNode (adjPos
		}

		#endregion

		#region Helper Methods

		// finds the closest hero in relation to the bot (myHero)
		// -x value is to the left or west
		// -y value is below or to the south
		// IMPORTANT: null Pos means there were no beatable heroes found (in other words they all have greater health then myHero)
		private Pos findClosestBeatableHeroPos () {
			Hero myHero = serverStuff.myHero;
			Pos closestBeatableHeroPos = null;
			foreach (Hero currHero in serverStuff.heroes) {
				if (myHero.life > currHero.life) {
					if (closestBeatableHeroPos == null || myHero.pos.Distance (currHero.pos) < myHero.pos.Distance (closestBeatableHeroPos)) {
						closestBeatableHeroPos = currHero.pos;
					}
				}
			}

			return closestBeatableHeroPos;
		}

		// finds the closest mine in relation to the bot (myHero)
		// -x value is to the left or west
		// -y value is below or to the south
		// IMPORTANT: null Pos means there were no neutral mines found (in other words they're all controlled)
		private Pos findClosestNeutralMinePos () {
			Pos myHeroPos = serverStuff.myHero.pos;
			Pos closestNeutralMinePos = null;
			foreach (Pos currMinePos in minesPosList) {
				if (closestNeutralMinePos == null || myHeroPos.Distance (currMinePos) < myHeroPos.Distance (closestNeutralMinePos)) {
					closestNeutralMinePos = currMinePos;
				}
			}

			return closestNeutralMinePos;
		}

		// finds the closest tavern in relation to the bot (myHero)
		// -x value is to the left or west
		// -y value is below or to the south
		// IMPORTANT: should never be the case but null Pos means there were no taverns found (in other words this map doesn't have any taverns)
		private Pos findClosestTavernPos () {
			Pos myHeroPos = serverStuff.myHero.pos;
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
		// -y value is below or to the south
		private Pos offsetToTilePos (Pos tilePos) {
			Pos myHeroPos = serverStuff.myHero.pos;
			Pos distPos = new Pos ();
			distPos.x = tilePos.x - myHeroPos.x;
			distPos.y = myHeroPos.y - tilePos.y;
		}

		// determines the tile for an offset from the bot's (myHero) position
		private Tile tileForOffset (int x, int y)
		{
			x += serverStuff.myHero.pos.x;
			y += serverStuff.myHero.pos.y;

			// make sure we're not outside the map
			if (x >= serverStuff.board.Length) {
				x = serverStuff.board.Length - 1;
			} else if (x < 0) {
				x = 0;
			}
			if (y >= serverStuff.board [x].Length) {
				y = serverStuff.board [x].Length - 1;
			} else if (y < 0) {
				y = 0;
			}

			return serverStuff.board [x] [y];
		}

		#endregion
    }
}
