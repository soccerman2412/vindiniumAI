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
		private const int minInterestedHeroWeight = 5;

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
			if (serverStuff != null) {
				serverStuff.createGame ();

				if (serverStuff.errored == false) {
					//opens up a webpage so you can view the game, doing it async so we dont time out
					new Thread (delegate() {
						System.Diagnostics.Process.Start (serverStuff.viewURL);
					}).Start ();

					Console.Out.WriteLine ("serverStuff.maxTurns: " + serverStuff.maxTurns);
					Console.Out.WriteLine ("my Hero (" + serverStuff.myHero.id + ") start position: " + correctedHeroPos ().x + ", " + correctedHeroPos ().y);
					foreach (Hero currHero in serverStuff.heroes) {
						Pos heroPos = currHero.GetCorrectedHeroPos ();
						Console.Out.WriteLine ("Hero (" + currHero.id + ") start position: " + heroPos.x + ", " + heroPos.y);
					}

					// memorize key aspects of the map
					memorizeMap ();
				}

				while (serverStuff.finished == false && serverStuff.errored == false) {
					string chosenMove = determineMove ();
					Console.Out.WriteLine ("chosenMove: " + chosenMove);
					serverStuff.moveHero (chosenMove);

					Console.Out.WriteLine ("completed turn " + serverStuff.currentTurn);
				}

				if (serverStuff.errored) {
					Console.Out.WriteLine ("error: " + serverStuff.errorText);
				}

				Console.Out.WriteLine ("bot finished");
			} else {
				Console.Out.WriteLine ("serverStuff was null, bot never started");
			}
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
			 * maybe check closest player near mine and health compared to my hero's health (are they near a tavern?)
			 * maybe check if you can swing by a tavern on the way to a particular target
			 * maybe check if we're being attacked, if so WHAT SHOULD WE DO?
			 */




			// check how many moves are left

			// check nearby tiles


			// make sure we're not currently looking to heal
			if (currentState != MyHeroState.HEAL) {
				/* Decision Tree:
				 *    1) Find the closest of 3 mines based on the path
				 *    2) Look for a nearby hero of interest (easy to kill, higher rank, )
				 *      a) if found check the path in relation to the current mine path
				 *        - if the path is less then mine path plus 5, attack the hero
				 *    3) Check if my hero's heal is less then 20 after taking the current path into account OR if we're in 1st near the end of the game
				 *      a) if the heal is less then 20 OR we're in 1st near the end of the game, we'll head for a tavern
				 */

				// trying to cut back on calls to A* path finding
				bool attacking = false;
				if (currentState == MyHeroState.ATTACK) {
					if (pathList.Count >= 5 && pathList.Count % 3 == 0)
						attacking = true;
					else if (pathList.Count % 2 == 0)
						attacking = true;
				}

				Pos heroOfInterestPos = null;
				List<string> bestPathToHeroOfInterest = new List<string> ();
				if (!attacking) {
					bestPathToHeroOfInterest = bestPathForClosestHeroOfInterestPos (out heroOfInterestPos);
					// check if we were attacking and the current hero of interest is relatively close to our target position
					attacking = heroOfInterestPos != null && bestPathToHeroOfInterest.Count > 0;
					if (attacking && currentTargetPos != null && pathList.Count > 0) {
						attacking = bestPathToHeroOfInterest.Count <= pathList.Count + 5;
					}
				}
					
				if (!attacking && currentState != MyHeroState.ATTACK) {
					// make sure we're not already capturing a mine
					if (currentState != MyHeroState.CAPTURE_MINE) {
						//Console.Out.WriteLine ("findClosestMinePos");
						List<Pos> closestMinePosList = findClosestMinePositions (3);
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
				} else if (heroOfInterestPos != null) {
					currentState = MyHeroState.ATTACK;
					if (currentTargetPos == null || !currentTargetPos.EqualsPos (heroOfInterestPos)) {
						currentTargetPos = heroOfInterestPos;
						Console.Out.WriteLine ("currentTargetPos: " + currentTargetPos.x + ", " + currentTargetPos.y);
						// wipe our current path and re-determine it
						pathList.Clear ();

						pathList = bestPathToHeroOfInterest;
					}
				}


				//Console.Out.WriteLine ("serverStuff.board.Rank: " + serverStuff.board.Rank);

				// 1) Check my hero's health, if the health minus the moves to the target is less than 20 we'll need to heal first
				// 2) Check if my hero is in first near the end of the game, if so we'll "suck our thumb"
				bool rank1NearGameEnd = false;//serverStuff.board.Rank == 1 && myHero.gold > 0 && (float)serverStuff.currentTurn / (float)serverStuff.maxTurns >= 0.95f;
				if (myHero.life - pathList.Count <= 20 || rank1NearGameEnd) {
					Console.Out.WriteLine ("myHero.life: " + myHero.life);
					Console.Out.WriteLine ("rank1NearGameEnd: " + rank1NearGameEnd);

					List<string> bestPathList = new List<string> ();
					Pos closestTavernPos = null;
					bestPathList = bestPathToClosestTavern (out closestTavernPos);

					if (closestTavernPos == null) {
						Console.Out.WriteLine ("closestTavernPos was null");
					} else if (currentTargetPos == null || !currentTargetPos.EqualsPos (closestTavernPos)) {
						currentState = MyHeroState.HEAL;

						currentTargetPos = closestTavernPos;
						Console.Out.WriteLine ("currentTargetPos: " + currentTargetPos.x + ", " + currentTargetPos.y);
						// wipe our current path and re-determine it
						pathList.Clear ();

						pathList = bestPathList;
					}
				}
			}

			if (pathList.Count > 0) {
				if (currentState == MyHeroState.HEAL && pathList.Count == 1) {
					// we're about to enter a tavern, let's see if we should heal up more than once
					// using 49 because the tavern will heal us by 50, but each turn we lose 1 health
					// check against 140 so we'll always heal up to at least 90 of the 100 (can't go over 100)
					if (myHero.life + 49 < 140) {
						pathList.Add (pathList [0]);
					}
				} else {
					// TODO: check if there's another hero nearby that could kill us
					if (currentState != MyHeroState.HEAL) {
						// we're near a tavern so we might as well heal up
						// using 49 because the tavern will heal us by 50, but each turn we lose 1 health
						// check against 140 so we'll always heal up to at least 90 of the 100 (can't go over 100)
						Pos myHeroPos = myHero.GetCorrectedHeroPos ();
						List<Pos> closestTavernPosList = findClosestTavernPositions ();
						if (closestTavernPosList.Count > 0) {
							Pos closestTavernPos = closestTavernPosList [0];
							if (myHeroPos.Distance (closestTavernPos) == 1 && myHero.life + 49 < 140) {
								if (myHeroPos.x < closestTavernPos.x) {
									pathList.Insert (0, Direction.East);
								} else if (myHeroPos.x > closestTavernPos.x) {
									pathList.Insert (0, Direction.West);
								} else if (myHeroPos.y > closestTavernPos.y) {
									pathList.Insert (0, Direction.North);
								} else if (myHeroPos.y < closestTavernPos.y) {
									pathList.Insert (0, Direction.South);
								}
							}
						}

						// check if we're near a mine
						List<Pos> closestMinePosList = findClosestMinePositions ();
						if (closestMinePosList.Count > 0) {
							Pos closestMinePos = closestMinePosList [0];
							if (myHeroPos.Distance (closestMinePos) == 1 && myHero.life > 20) {
								if (myHeroPos.x < closestMinePos.x) {
									pathList.Insert (0, Direction.East);
								} else if (myHeroPos.x > closestMinePos.x) {
									pathList.Insert (0, Direction.West);
								} else if (myHeroPos.y > closestMinePos.y) {
									pathList.Insert (0, Direction.North);
								} else if (myHeroPos.y < closestMinePos.y) {
									pathList.Insert (0, Direction.South);
								}
							}
						}

					}
				}

				returnVal = pathList [0];
				// if there's a hero in the spot we're about to move to then don't remove the path order as we'll need again until that other hero moves
				string avoidOrder = returnVal;
				if (currentState != MyHeroState.ATTACK) {
					avoidOrder = avoidHeroWithinDistance (2);
				}

				if (avoidOrder != null && returnVal != avoidOrder) {
					returnVal = avoidOrder;
					pathList.Insert (0, oppositeDirection (returnVal));
				} else {
					pathList.RemoveAt (0);
				}

				/*Hero nextDoorHero = nextMoveDirectionHasHero (returnVal);
				if (nextDoorHero == null)
					pathList.RemoveAt (0);
				else {
					if (currentState == MyHeroState.HEAL) {
						returnVal = avoidanceCorrection (returnVal);
						pathList.Insert (0, oppositeDirection (returnVal));
					} else if (currentState != MyHeroState.ATTACK) {
						if (myHero.life < nextDoorHero.life || weightHeroInterest (nextDoorHero) < 3) {
							returnVal = avoidanceCorrection (returnVal);
							pathList.Insert (0, oppositeDirection (returnVal));
						}
					}
				}*/

				// check if we reached then end of our path and hopefully our target
				if (pathList.Count == 0) {
					currentState = MyHeroState.NONE;
					currentTargetPos = null;
				}
			}

			return returnVal;
		}

		private string avoidHeroWithinDistance (int dist = 1) {
			Hero myHero = serverStuff.myHero;
			Pos myHeroPos = myHero.GetCorrectedHeroPos ();
			Dictionary<string, int> weightedDirectionsDict = new Dictionary<string, int> {
				[Direction.East]=0,
				[Direction.West]=0,
				[Direction.North]=0,
				[Direction.South]=0
			};
			int heroesToAvoid = 0;
			foreach (Hero currHero in serverStuff.heroes) {
				if (currHero.id == myHero.id)
					continue;

				Pos currHeroPos = currHero.GetCorrectedHeroPos ();

				if (myHeroPos.Distance (currHeroPos) <= dist) {
					++heroesToAvoid;

					if (heroesToAvoid > 1 || myHero.life < currHero.life || weightHeroInterest (currHero) < minInterestedHeroWeight) {
						int diffX = myHeroPos.x - currHeroPos.x;
						int diffY = myHeroPos.y - currHeroPos.y;
						if (diffX == 0) {
							weightedDirectionsDict [Direction.East] += 1;
							weightedDirectionsDict [Direction.West] += 1;
							if (diffY < 0)
								weightedDirectionsDict [Direction.North] += 2;
							else
								weightedDirectionsDict [Direction.South] += 2;
						} else if (diffY == 0) {
							weightedDirectionsDict [Direction.North] += 1;
							weightedDirectionsDict [Direction.South] += 1;
							if (diffX > 0)
								weightedDirectionsDict [Direction.East] += 2;
							else
								weightedDirectionsDict [Direction.West] += 2;
						} else {
							if (diffX > 0)
								weightedDirectionsDict [Direction.East] += 1;
							else
								weightedDirectionsDict [Direction.West] += 1;

							if (diffY < 0)
								weightedDirectionsDict [Direction.North] += 1;
							else
								weightedDirectionsDict [Direction.South] += 1;
						}
					}
				}
			}

			if (heroesToAvoid > 0) {
				List<KeyValuePair<string, int>> sortedList = weightedDirectionsDict.ToList ();

				sortedList.OrderByDescending (x => x.Value);

				Console.Out.WriteLine ("Sorted avoid directions: ");
				foreach (KeyValuePair<string, int> currPair in sortedList) {
					Console.Out.WriteLine (currPair.Key + "with value: " + currPair.Value);
				}
				Console.Out.WriteLine ("Sorted avoid directions end");

				int directionsWithWeight = 0;
				Tile currTile = Tile.HERO_1;
				foreach (KeyValuePair<string, int> currPair in sortedList) {
					if (currPair.Value > 0) {
						++directionsWithWeight;

						switch (currPair.Key) {
						case Direction.East:
							currTile = tileForCoords (myHeroPos.x + 1, myHeroPos.y);
							if (currTile == Tile.FREE || currTile == Tile.TAVERN)
								return Direction.East;
							break;
						case Direction.West:
							currTile = tileForCoords (myHeroPos.x - 1, myHeroPos.y);
							if (currTile == Tile.FREE || currTile == Tile.TAVERN)
								return Direction.West;
							break;
						case Direction.North:
							currTile = tileForCoords (myHeroPos.x, myHeroPos.y - 1);
							if (currTile == Tile.FREE || currTile == Tile.TAVERN)
								return Direction.North;
							break;
						case Direction.South:
							currTile = tileForCoords (myHeroPos.x, myHeroPos.y + 1);
							if (currTile == Tile.FREE || currTile == Tile.TAVERN)
								return Direction.South;
							break;
						default:
							break;
						}
					}
				}

				// found a nearby hero but couldn't find a free avoidance location
				if (directionsWithWeight > 0) {
					Console.Out.WriteLine ("avoidHeroWithinDistance Direction.Stay");
					return Direction.Stay;
				}
			}

			return null;
		}

		private string oppositeDirection (string directionToOppose) {
			switch (directionToOppose) {
			case Direction.East:
				return Direction.West;
			case Direction.West:
				return Direction.East;
			case Direction.North:
				return Direction.South;
			case Direction.South:
				return Direction.North;
			default:
				break;
			}
				
			Console.Out.WriteLine ("oppositeDirection Direction.Stay");
			return Direction.Stay;
		}

		private string avoidanceCorrection (string directionToAvoid) {
			Pos myHeroPos = serverStuff.myHero.GetCorrectedHeroPos ();

			Tile currTile = Tile.HERO_1;
			switch (directionToAvoid) {
			case Direction.East:
				currTile = tileForCoords (myHeroPos.x, myHeroPos.y - 1);
				if (currTile == Tile.FREE || currTile == Tile.TAVERN)
					return Direction.North;
				currTile = tileForCoords (myHeroPos.x, myHeroPos.y + 1);
				if (currTile == Tile.FREE || currTile == Tile.TAVERN)
					return Direction.South;
				return Direction.West;
			case Direction.West:
				currTile = tileForCoords (myHeroPos.x, myHeroPos.y - 1);
				if (currTile == Tile.FREE || currTile == Tile.TAVERN)
					return Direction.North;
				currTile = tileForCoords (myHeroPos.x, myHeroPos.y + 1);
				if (currTile == Tile.FREE || currTile == Tile.TAVERN)
					return Direction.South;
				return Direction.East;
			case Direction.North:
				currTile = tileForCoords (myHeroPos.x + 1, myHeroPos.y);
				if (currTile == Tile.FREE || currTile == Tile.TAVERN)
					return Direction.East;
				currTile = tileForCoords (myHeroPos.x - 1, myHeroPos.y);
				if (currTile == Tile.FREE || currTile == Tile.TAVERN)
					return Direction.West;
				return Direction.South;
			case Direction.South:
				currTile = tileForCoords (myHeroPos.x + 1, myHeroPos.y);
				if (currTile == Tile.FREE || currTile == Tile.TAVERN)
					return Direction.East;
				currTile = tileForCoords (myHeroPos.x - 1, myHeroPos.y);
				if (currTile == Tile.FREE || currTile == Tile.TAVERN)
					return Direction.West;
				return Direction.North;
			default:
				break;
			}

			Console.Out.WriteLine ("avoidanceCorrection Direction.Stay");
			return Direction.Stay;
		}

		private Hero nextMoveDirectionHasHero (string moveDirection) {
			Pos myHeroPos = serverStuff.myHero.GetCorrectedHeroPos ();
			Tile destTile = Tile.FREE;

			switch (moveDirection) {
			case Direction.East:
				destTile = tileForCoords (myHeroPos.x + 1, myHeroPos.y);
				break;
			case Direction.West:
				destTile = tileForCoords (myHeroPos.x - 1, myHeroPos.y);
				break;
			case Direction.North:
				destTile = tileForCoords (myHeroPos.x, myHeroPos.y - 1);
				break;
			case Direction.South:
				destTile = tileForCoords (myHeroPos.x, myHeroPos.y + 1);
				break;
			default:
				break;
			}

			if (destTile == Tile.HERO_1) {
				Console.Out.WriteLine ("destination tile is Tile.HERO_1 and serverStuff.heroes [0]: " + serverStuff.heroes [0].id);
				return serverStuff.heroes [0];
			} else if (destTile == Tile.HERO_2) {
				Console.Out.WriteLine ("destination tile is Tile.HERO_2 and serverStuff.heroes [1]: " + serverStuff.heroes [1].id);
				return serverStuff.heroes [1];
			} else if (destTile == Tile.HERO_3) {
				Console.Out.WriteLine ("destination tile is Tile.HERO_3 and serverStuff.heroes [2]: " + serverStuff.heroes [2].id);
				return serverStuff.heroes [2];
			} else if (destTile == Tile.HERO_4) {
				Console.Out.WriteLine ("destination tile is Tile.HERO_4 and serverStuff.heroes [3]: " + serverStuff.heroes [3].id);
				return serverStuff.heroes [3];
			}

			return null;
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
			openList = openList.OrderBy (x => x.aStarF).ToList ();
			/*openList.Sort (delegate (PathNode a, PathNode b) {
				return a.aStarF.CompareTo (b.aStarF);
			});*/

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

		private double weightHeroInterest (Hero heroInQuestion) {
			Hero myHero = serverStuff.myHero;

			double gameDonePercent = (double)serverStuff.currentTurn / (double)serverStuff.maxTurns;
			double currHeroWeight = 0;
			currHeroWeight += ((double)(heroInQuestion.gold - myHero.gold) / 10) * gameDonePercent;
			currHeroWeight += (double)(heroInQuestion.mineCount - myHero.mineCount) * gameDonePercent;
			currHeroWeight += myHero.pos.Distance (heroInQuestion.pos) / ((double)serverStuff.board.Length / 2); // minimal weight since obstacles can drastically effect the path distance
			// having less health then the currHero should weigh more heavily then having more health
			double healthDiff = myHero.life - heroInQuestion.life;
			if (healthDiff < 0)
				currHeroWeight += healthDiff / 2;
			else
				currHeroWeight += healthDiff / 25;

			return currHeroWeight;
		}

		// finds the closest hero of interest in relation to the bot (myHero)
		// out heroOfInterestPos is null if no hero of interest is found (or if the path finding failed, which would be a bug)
		// hero intest based on weight (see weigh explanation in method)
		// IMPORTANT: empty path list means there were no hero of interest found (or if the path finding failed, which would be a bug)
		private List<string> bestPathForClosestHeroOfInterestPos (out Pos heroOfInterestPos) {
			heroOfInterestPos = null;
			List<string> returnPath = new List<string> ();

			Hero myHero = serverStuff.myHero;

			// weigh our heros based on:
			// their health vs ours
			// their gold vs ours in relation to the turns left
			// their mines vs ours in relation to the turns left
			double bestHeroOfInterestWeight = 0;
			Hero heroOfInterest = null;
			foreach (Hero currHero in serverStuff.heroes) {
				// skip over myhero
				if (currHero.id == myHero.id) {
					continue;
				}

				double currHeroWeight = weightHeroInterest (currHero);

				if (bestHeroOfInterestWeight < currHeroWeight) {
					bestHeroOfInterestWeight = currHeroWeight;
					heroOfInterest = currHero;
				}
			}
				
			if (heroOfInterest != null && bestHeroOfInterestWeight >= minInterestedHeroWeight) {
				Pos startPos = myHero.GetCorrectedHeroPos ();
				heroOfInterestPos = heroOfInterest.GetCorrectedHeroPos ();

				// do we have enough health to beat this hero
				if (myHero.life - heroOfInterest.life < 1) {
					// we'll need to find a tavern first
					Pos closestTavernPos = null;
					returnPath = bestPathToClosestTavern (out closestTavernPos);

					if (closestTavernPos != null)
						startPos = closestTavernPos;
				}

				List<string> bestPathToHeroOfInterest = new List<string> ();
				bestPathFound = false;
				closedList.Clear ();
				if (findBestPathFromPosToPos (startPos, heroOfInterestPos, out bestPathToHeroOfInterest))
					returnPath.AddRange (bestPathToHeroOfInterest);
				else {
					heroOfInterestPos = null;
					returnPath.Clear ();
				}
			}

			return returnPath;




//			Pos myHeroPos = correctedHeroPos ();
//			Pos closestBeatableHeroPos = null;
//			int greatestDiffInGold = 0;
//			double currBeatableHeroDist = 99999;
//			foreach (Hero currHero in serverStuff.heroes) {
//				if (currHero.id != myHero.id) {
//					// dist
//					double currHeroDist = myHeroPos.Distance (currHero.GetCorrectedHeroPos ());
//					// does this hero have more gold than me by at least 250
//					bool otherHeroHasTooMuchMoreGold = currHero.gold - myHero.gold >= 250;
//					// does this hero have less health then me
//					bool otherHeroHasLessHealth = myHero.life > currHero.life;
//
//					/* TODO:
//					 * check if other hero has a decent amount more gold diff:
//					 * -- check if they have more health and heal up before attacking
//					 * */
//
//					// best case scenario
//					if (otherHeroHasLessHealth && otherHeroHasTooMuchMoreGold) {
//						List<string> currPath
//					}
//					if (otherHeroHasTooMuchMoreGold) {
//
//					if (!otherHeroHasLessHealth) {
//						// other hero does
//					}
//				}
//			}
//
//			return closestBeatableHeroPos;
		}

		// finds the closest mine(s) in relation to the bot (myHero)
		// argument amountToFind: amount of mines to compare/find
		// retuns a list of board position(s) of the sorted mine(s) based on distance
		// IMPORTANT: null Pos means there were no mines found (in other words my hero controls them all)
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

		// finds the best/shortest path to a tavern
		// out closestTavernPos is null if no tavern is found (or if the path finding failed, which would be a bug)
		// retuns the path list of string moves
		// IMPORTANT: empty path list means there was no tavern found (or if the path finding failed, which would be a bug)
		private List<string> bestPathToClosestTavern (out Pos closestTavernPos) {
			closestTavernPos = null;
			List<string> bestPathList = new List<string> ();

			// find a tavern
			List<Pos> closestTavernPosList = findClosestTavernPositions (2);
			if (closestTavernPosList.Count > 0) {
				//Console.Out.WriteLine ("findClosestTavernPositions count greater then 0");

				// find the best path for each tavern so we can compare which one is truly closest
				int smallestPathLength = 99999;
				foreach (Pos currTavernPos in closestTavernPosList) {
					List<string> currBestPath = bestPathToPos (currTavernPos);
					if (currBestPath.Count < smallestPathLength) {
						smallestPathLength = currBestPath.Count;
						bestPathList = currBestPath;
						closestTavernPos = currTavernPos;
					}
				}

			}

			return bestPathList;
		}

		// finds the closest tavern(s) in relation to the bot (myHero)
		// argument amountToFind: amount of taverns to compare/find
		// retuns a list of board position(s) of the sorted tavern(s) based on distance
		// IMPORTANT: should never be the case but null Pos means there were no taverns found (in other words this map doesn't have any taverns)
		private List<Pos> findClosestTavernPositions (int amountToFind = 1) {
			Pos myHeroPos = correctedHeroPos ();

			// sort the list of the mines in relation to the current hero's pos
			List <Pos> sortedList = new List<Pos> (tavernsPosList);
			sortedList.Sort (delegate (Pos a, Pos b) {
				return myHeroPos.Distance(a).CompareTo (myHeroPos.Distance(b));
			});

			if (amountToFind > sortedList.Count) {
				return sortedList;
			}

			return sortedList.GetRange (0, amountToFind);
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
