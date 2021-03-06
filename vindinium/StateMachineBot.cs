﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vindinium
{
	static class Extensions
	{
		public static double Distance (this Pos thisPos, Pos otherPos, bool useManhattan = false) {
			// check if we should use the Manhattan Distance formula
			if (useManhattan)
				return Math.Abs (thisPos.x - otherPos.x) + Math.Abs (thisPos.y - otherPos.y);

			// Euclidean Distance
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
		private const double millisecondBailThreshold = 950;

		private ServerStuff serverStuff = null;

		private Hero myHero = null;
		private Pos myHeroPos = null;
		private MyHeroState currentState = MyHeroState.NONE;

		private Hero firstPlaceHero = null;
		private Hero secondPlaceHero = null;

		private bool isWorthAttacking = true;

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
			//Console.Out.WriteLine("State Machine Bot running");
			if (serverStuff != null) {
				serverStuff.createGame ();

				/*if (serverStuff.errored == false) {
					// try one more time
					serverStuff.createGame ();
				}*/

				if (serverStuff.errored == false) {
					//opens up a webpage so you can view the game, doing it async so we dont time out
					new Thread (delegate() {
						System.Diagnostics.Process.Start (serverStuff.viewURL);
					}).Start ();

					myHero = serverStuff.myHero;
					myHeroPos = myHero.GetCorrectedHeroPos ();

					if (minesPosList.Count < 12 && serverStuff.board.Length < 14) {
						isWorthAttacking = false;
					}

					Console.Out.WriteLine ("serverStuff.maxTurns: " + serverStuff.maxTurns);
					Console.Out.WriteLine ("my Hero (" + myHero.id + ") start position: " + myHeroPos.x + ", " + myHeroPos.y);

					foreach (Hero currHero in serverStuff.heroes) {
						Pos heroPos = currHero.GetCorrectedHeroPos ();
						Console.Out.WriteLine ("Hero (" + currHero.id + ") start position: " + heroPos.x + ", " + heroPos.y);
					}

					// memorize key aspects of the map
					memorizeMap ();
				}

				while (serverStuff.finished == false && serverStuff.errored == false) {
					// update vars
					myHero = serverStuff.myHero;
					myHeroPos = myHero.GetCorrectedHeroPos ();

					firstPlaceHero = null;
					secondPlaceHero = null;
					foreach (Hero currHero in serverStuff.heroes) {
						if (firstPlaceHero == null || currHero.gold > firstPlaceHero.gold) {
							secondPlaceHero = firstPlaceHero;
							firstPlaceHero = currHero;
						} else if (currHero.gold == firstPlaceHero.gold && currHero.mineCount > firstPlaceHero.mineCount) {
							secondPlaceHero = firstPlaceHero;
							firstPlaceHero = currHero;
						} else if (secondPlaceHero == null || currHero.gold > secondPlaceHero.gold) {
							secondPlaceHero = currHero;
						} else if (currHero.gold == secondPlaceHero.gold && currHero.mineCount > secondPlaceHero.mineCount) {
							secondPlaceHero = currHero;
						}
					}

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
					case Tile.GOLD_MINE_1:
					case Tile.GOLD_MINE_2:
					case Tile.GOLD_MINE_3:
					case Tile.GOLD_MINE_4:
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



		#region Move Determination

		private string determineMove ()
		{
			// for handling time out
			DateTime startUtc = DateTime.UtcNow;
			bool takingTooLong = false;
			List<string> bestPathList = new List<string> ();

			// default move
			string returnVal = Direction.Stay;

			// current closest taverns and mines
			List<Pos> closestMinePosList = findClosestMinePositions (3);
			List<Pos> closestTavernPosList = findClosestTavernPositions (3);

			// check if we've gotten to the target (doesn't really happen unless the target pos is a free tile)
			if (currentTargetPos != null && myHeroPos.EqualsPos (currentTargetPos)) {
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
			 * maybe weigh the map based on hero positions and go to an area that's less populated
			 * fine tune avoidance (or don't use?)
			 * maybe check if we're being attacked, if so WHAT SHOULD WE DO?
			 */



			// check how many moves are left

			// check nearby tiles



			// check if we're in 1st place
			bool inFirstPlace = false;
			if (firstPlaceHero != null) {
				inFirstPlace = firstPlaceHero.id == myHero.id;
			}

			Pos heroOfInterestPos = null;
			List<string> bestPathToHeroOfInterest = new List<string> ();
			bool attacking = false;

			switch (currentState) {

			case MyHeroState.HEAL:
				break;

			case MyHeroState.CAPTURE_MINE:
				break;

			case MyHeroState.ATTACK:
				// trying to cut back on calls to A* path finding
				if (pathList.Count >= 5 && pathList.Count % 3 != 0)
					attacking = true;
				else if (pathList.Count % 2 == 0)
					attacking = true;
				break;

			default:
				break;
			}


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

				if (myHero.life - pathList.Count > 20 && !inFirstPlace) {
					if (currentState == MyHeroState.ATTACK) {
						if (pathList.Count >= 5 && pathList.Count % 3 != 0)
							attacking = true;
						else if (pathList.Count % 2 == 0)
							attacking = true;
					}

					if (isWorthAttacking && !attacking && currentState != MyHeroState.CAPTURE_MINE) {
						bestPathToHeroOfInterest = bestPathForClosestHeroOfInterestPos (out heroOfInterestPos, closestTavernPosList);
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
							if (closestMinePosList.Count > 0) {
								Console.Out.WriteLine ("findClosestMinePos count greater then 0");

								// find the best path for each mine so we can compare which one is truly closest
								Pos closestMinePos = null;
								bestPathList = new List<string> ();
								bestPathList = bestPathToClosestMine (out closestMinePos, closestMinePosList);


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
				}
					
				// check our time
				takingTooLong = DateTime.UtcNow.Subtract (startUtc).Milliseconds > millisecondBailThreshold;
				if (takingTooLong) {
					Console.Out.WriteLine ("takingTooLong, not going to check health");
				}

				//Console.Out.WriteLine ("serverStuff.board.Rank: " + serverStuff.board.Rank);

				// 1) Check my hero's health, if the health minus the moves to the target is less than 20 we'll need to heal first
				// 2) Check if my hero is in first near the end of the game, if so we'll "suck our thumb"
				bool rank1NearGameEnd = inFirstPlace && (float)serverStuff.currentTurn / (float)serverStuff.maxTurns >= 0.95f;
				bool inFirstPlaceByALot = inFirstPlace && firstPlaceHero.gold - secondPlaceHero.gold > (serverStuff.maxTurns * 0.25) * (minesPosList.Count * 0.125);
				bool shouldHeal = inFirstPlaceByALot || (myHero.life - pathList.Count <= 20) || rank1NearGameEnd;
				if (!takingTooLong && shouldHeal) {
					Console.Out.WriteLine ("inFirstPlace: " + inFirstPlace);
					Console.Out.WriteLine ("myHero.life: " + myHero.life);
					Console.Out.WriteLine ("myHero.life - pathList.Count: " + (myHero.life - pathList.Count));
					Console.Out.WriteLine ("rank1NearGameEnd: " + rank1NearGameEnd);

					bestPathList = new List<string> ();
					Pos closestTavernPos = null;
					bestPathList = bestPathToClosestTavern (out closestTavernPos, closestTavernPosList);

					// if we're headed for a mine and it's closer than a tavern and we don't currently have a lot of mines simply commit suicide
					/*int minThreshold = (int)Math.Round ((double)minesPosList.Count * 0.1);
					minThreshold = minThreshold < 2 ? 2 : minThreshold;
					if (currentState == MyHeroState.CAPTURE_MINE && pathList.Count < bestPathList.Count && myHero.mineCount <= minThreshold) {
						Console.Out.WriteLine ("low on health but mine is closer than tavern");
					} else*/ if (closestTavernPos == null) {
						Console.Out.WriteLine ("closestTavernPos was null");
					} else if (bestPathList.Count == 0) {
						Console.Out.WriteLine ("bestPathList.Count to tavern is 0");
					} else if (currentTargetPos == null || !currentTargetPos.EqualsPos (closestTavernPos)) {
						currentState = MyHeroState.HEAL;

						currentTargetPos = closestTavernPos;
						Console.Out.WriteLine ("currentTargetPos: " + currentTargetPos.x + ", " + currentTargetPos.y);
						// wipe our current path and re-determine it
						pathList.Clear ();

						pathList = bestPathList;
					}

					// check our time
					/*takingTooLong = DateTime.UtcNow.Subtract (startUtc).Milliseconds > millisecondBailThreshold;
					if (takingTooLong) {
						Console.Out.WriteLine ("takingTooLong, not going to check suicide mine");
					}
					// if we have time we'll check if a mine is closer
					if (!takingTooLong && currentState != MyHeroState.CAPTURE_MINE) {
						bestPathList = new List<string> ();
						Pos closestMinePos = null;
						bestPathList = bestPathToClosestMine (out closestMinePos, closestMinePosList);

						if (closestMinePos != null && bestPathList.Count < pathList.Count && myHero.mineCount <= minThreshold) {
							currentState = MyHeroState.CAPTURE_MINE;

							currentTargetPos = closestMinePos;
							Console.Out.WriteLine ("mine closer then tavern, currentTargetPos: " + currentTargetPos.x + ", " + currentTargetPos.y);
							// wipe our current path and re-determine it
							pathList.Clear ();

							pathList = bestPathList;
						}
					}*/
				}
			}

			if (pathList.Count > 0) {
				bool shouldNotAvoid = false;

				// check our time
				/*takingTooLong = DateTime.UtcNow.Subtract (startUtc).Milliseconds > millisecondBailThreshold;
				if (takingTooLong) {
					Console.Out.WriteLine ("takingTooLong");
					shouldNotAvoid = true;
				}*/

				if (currentState == MyHeroState.HEAL && pathList.Count <= 2) {
					// we're about to enter a tavern, let's see if we should heal up more than once
					// using 49 because the tavern will heal us by 50, but each turn we lose 1 health
					// check against 140 so we'll always heal up to at least 90 of the 100 (can't go over 100)
					if (myHero.life + 49 < 140) {
						shouldNotAvoid = true;
						pathList.Add (pathList [0]);
					}
				} else if (pathList.Count > 2) {
					// we're near a tavern so we might as well heal up
					// using 49 because the tavern will heal us by 50, but each turn we lose 1 health
					// check against 125 so we'll always heal up to at least 75 of the 100 (can't go over 100)
					if (closestTavernPosList.Count > 0) {
						Console.Out.WriteLine ("--closestTavernPosList.Count > 0");

						Pos closestTavernPos = closestTavernPosList [0];
						if (myHeroPos.Distance (closestTavernPos) == 1 && myHero.life + 49 < 125) {
							shouldNotAvoid = true;

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

					if (currentState != MyHeroState.HEAL) {
						// check if we're near a mine
						if (closestMinePosList.Count > 0 && currentState != MyHeroState.ATTACK) {
							Console.Out.WriteLine ("--closestMinePosList.Count > 0");

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
				Console.Out.WriteLine ("returnVal: " + returnVal);
				// if we're not within reach of our target and not attacking then check if we need to avoid (unless told to ignore avoid logic)
				string avoidOrder = returnVal;
				if (pathList.Count > 1 && currentState != MyHeroState.ATTACK && !shouldNotAvoid) {
					avoidOrder = avoidHeroWithinDistance (3);
					Console.Out.WriteLine ("avoidOrder: " + avoidOrder);
				}

				if (avoidOrder != null && !returnVal.Equals (avoidOrder)) {
					// if we're headed for a mine and it's close simply commit suicide so the other hero doesn't get our mines
					if (currentState == MyHeroState.CAPTURE_MINE && pathList.Count <= 2) {
						Console.Out.WriteLine ("avoiding but mine is close");
						shouldNotAvoid = true;
					}

					// check our time
					takingTooLong = DateTime.UtcNow.Subtract (startUtc).Milliseconds > millisecondBailThreshold;
					if (takingTooLong) {
						Console.Out.WriteLine ("takingTooLong, not going to check suicide mine");
					}
					// if we have time we'll check if a mine is close
					if (!takingTooLong && currentState != MyHeroState.CAPTURE_MINE) {
						bestPathList = new List<string> ();
						Pos closestMinePos = null;
						bestPathList = bestPathToClosestMine (out closestMinePos, closestMinePosList);

						if (closestMinePos != null && bestPathList.Count <= 2) {
							shouldNotAvoid = true;

							currentState = MyHeroState.CAPTURE_MINE;

							currentTargetPos = closestMinePos;
							Console.Out.WriteLine ("mine closer then tavern, currentTargetPos: " + currentTargetPos.x + ", " + currentTargetPos.y);
							// wipe our current path and re-determine it
							pathList.Clear ();

							pathList = bestPathList;
						}
					}

					// after our suicide mine checking make sure we still want to avoid
					if (!shouldNotAvoid) {
						returnVal = avoidOrder;
						//pathList.Insert (0, oppositeDirection (returnVal));
						pathList.Clear ();
					}
				} else {
					pathList.RemoveAt (0);
				}
			}

			if (returnVal.Equals (Direction.Stay)) {
				Console.Out.WriteLine ("currentState: " + currentState);
				Console.Out.WriteLine ("pathList.Count: " + pathList.Count);
				if (currentTargetPos != null) {
					Console.Out.WriteLine ("currentTargetPos: " + currentTargetPos.x + ", " + currentTargetPos.y);
				}
			}

			// check if we reached then end of our path and hopefully our target
			if (pathList.Count == 0) {
				currentState = MyHeroState.NONE;
				currentTargetPos = null;
			}

			return returnVal;
		}

		private void HandleHealingState () {
		}

		private void HandleMiningState () {
		}

		private void HandleAttackingState () {
		}

		#endregion



		#region Avoidance Logic

		private string avoidHeroWithinDistance (double dist = 1) {
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

				if (myHeroPos.Distance (currHeroPos, true) <= dist) {
					++heroesToAvoid;

					if (heroesToAvoid > 1 || myHero.life < currHero.life) {
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

				sortedList = sortedList.OrderByDescending (x => x.Value).ToList ();

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

		#endregion



		#region Best Path Methods

		private bool bestPathFound = false;
		private List<PathNode> closedList = new List<PathNode> ();
		private List<string> bestPathToPos (Pos targetPos) {
			bestPathFound = false;
			closedList.Clear ();

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

		// finds the closest hero of interest in relation to the bot (myHero)
		// out heroOfInterestPos is null if no hero of interest is found (or if the path finding failed, which would be a bug)
		// hero intest based on weight (see weigh explanation in method)
		// IMPORTANT: empty path list means there were no hero of interest found (or if the path finding failed, which would be a bug)
		private List<string> bestPathForClosestHeroOfInterestPos (out Pos heroOfInterestPos, List<Pos> closestTavernPosList = null) {
			heroOfInterestPos = null;
			List<string> returnPath = new List<string> ();

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
				Pos startPos = myHeroPos;
				heroOfInterestPos = heroOfInterest.GetCorrectedHeroPos ();

				// do we have enough health to beat this hero
				if (myHero.life - heroOfInterest.life < 1) {
					// we'll need to find a tavern first
					Pos closestTavernPos = null;
					returnPath = bestPathToClosestTavern (out closestTavernPos, closestTavernPosList);

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
		}

		//
		private List<string> bestPathToClosestMine (out Pos closestMinePos, List<Pos> closestMinePosList = null) {
			closestMinePos = null;
			List<string> bestPathList = new List<string> ();

			// find a tavern
			if (closestMinePosList == null) {
				closestMinePosList = findClosestMinePositions (3);
			}

			if (closestMinePosList != null && closestMinePosList.Count > 0) {
				// find the best path for each mine so we can compare which one is truly closest
				int smallestPathLength = 99999;
				//int i = 0;
				foreach (Pos currMinePos in closestMinePosList) {
					// check our time
					/*takingTooLong = DateTime.UtcNow.Subtract (startUtc).Milliseconds > millisecondBailThreshold;
				if (takingTooLong) {
					Console.Out.WriteLine ("takingTooLong, not checking rest of mines in list. Check " + i + " of " + closestMinePosList.Count);
					break;
				}*/

					List<string> currBestPath = bestPathToPos (currMinePos);
					if (currBestPath.Count > 0 && currBestPath.Count < smallestPathLength) {
						smallestPathLength = currBestPath.Count;
						bestPathList = currBestPath;
						closestMinePos = currMinePos;
					}

					//++i;
				}

			}

			return bestPathList;
		}

		// finds the best/shortest path to a tavern
		// out closestTavernPos is null if no tavern is found (or if the path finding failed, which would be a bug)
		// retuns the path list of string moves
		// IMPORTANT: empty path list means there was no tavern found (or if the path finding failed, which would be a bug)
		private List<string> bestPathToClosestTavern (out Pos closestTavernPos, List<Pos> closestTavernPosList = null) {
			closestTavernPos = null;
			List<string> bestPathList = new List<string> ();

			// find a tavern
			if (closestTavernPosList == null) {
				closestTavernPosList = findClosestTavernPositions (3);
			}
			if (closestTavernPosList.Count > 0) {
				//Console.Out.WriteLine ("findClosestTavernPositions count greater then 0");

				// find the best path for each tavern so we can compare which one is truly closest
				int smallestPathLength = 99999;
				foreach (Pos currTavernPos in closestTavernPosList) {
					List<string> currBestPath = bestPathToPos (currTavernPos);
					if (currBestPath.Count > 0 && currBestPath.Count < smallestPathLength) {
						smallestPathLength = currBestPath.Count;
						bestPathList = currBestPath;
						closestTavernPos = currTavernPos;
					}
				}

			}

			return bestPathList;
		}

		#endregion



		#region Simple A Star

		private bool findBestPathFromPosToPos (Pos currentPos, Pos targetPos, out List<string> bestPath) {
			bestPath = new List<string> ();

			//Console.Out.WriteLine("findBestPathFromPosToPos=> currentPos: " + currentPos.x + ", " + currentPos.y);

			if (currentPos.EqualsPos (targetPos)) {
				bestPathFound = true;
				return true;
			} else if (bestPathFound)
				return false;

			bool ignoreAdjPositions = false;
			List<PathNode> openList = new List<PathNode> ();
			Pos adjPos = null;
			double g = 0;
			double h = 0;
			double f = g + h;
			PathNode adjNode = null;

			// only using directions that we can validly move to 

			// north
			adjPos = new Pos ();
			adjPos.x = currentPos.x;
			adjPos.y = currentPos.y - 1;
			Tile currTile = tileForCoords (adjPos.x, adjPos.y);
			g = currentPos.Distance (adjPos, true);
			h = adjPos.Distance (targetPos, true);
			f = g + h;
			adjNode = new PathNode (adjPos, f);
			if (adjPos.EqualsPos (targetPos)) {
				openList.Add (adjNode);
				ignoreAdjPositions = true;
			} else if (currTile == Tile.FREE || currTile == Tile.HERO_1 || currTile == Tile.HERO_2 ||
				currTile == Tile.HERO_3 || currTile == Tile.HERO_4) {
				openList.Add (adjNode);
			}

			if (!ignoreAdjPositions) {
				// east
				adjPos.x = currentPos.x + 1;
				adjPos.y = currentPos.y;
				currTile = tileForCoords (adjPos.x, adjPos.y);
				g = currentPos.Distance (adjPos, true);
				h = adjPos.Distance (targetPos, true);
				f = g + h;
				adjNode = new PathNode (adjPos, f);
				if (adjPos.EqualsPos (targetPos)) {
					openList.Clear (); // only need this one
					openList.Add (adjNode);
					ignoreAdjPositions = true;
				} else if (currTile == Tile.FREE || currTile == Tile.HERO_1 || currTile == Tile.HERO_2 ||
				          currTile == Tile.HERO_3 || currTile == Tile.HERO_4) {
					openList.Add (adjNode);
				}
			}

			if (!ignoreAdjPositions) {
				// south
				adjPos.x = currentPos.x;
				adjPos.y = currentPos.y + 1;
				currTile = tileForCoords (adjPos.x, adjPos.y);
				g = currentPos.Distance (adjPos, true);
				h = adjPos.Distance (targetPos, true);
				f = g + h;
				adjNode = new PathNode (adjPos, f);
				if (adjPos.EqualsPos (targetPos)) {
					openList.Clear (); // only need this one
					openList.Add (adjNode);
					ignoreAdjPositions = true;
				} else if (currTile == Tile.FREE || currTile == Tile.HERO_1 || currTile == Tile.HERO_2 ||
				          currTile == Tile.HERO_3 || currTile == Tile.HERO_4) {
					openList.Add (adjNode);
				}
			}

			if (!ignoreAdjPositions) {
				// west
				adjPos.x = currentPos.x - 1;
				adjPos.y = currentPos.y;
				currTile = tileForCoords (adjPos.x, adjPos.y);
				g = currentPos.Distance (adjPos, true);
				h = adjPos.Distance (targetPos, true);
				f = g + h;
				adjNode = new PathNode (adjPos, f);
				if (adjPos.EqualsPos (targetPos)) {
					openList.Clear (); // only need this one
					openList.Add (adjNode);
					ignoreAdjPositions = true;
				} else if (currTile == Tile.FREE || currTile == Tile.HERO_1 || currTile == Tile.HERO_2 ||
				          currTile == Tile.HERO_3 || currTile == Tile.HERO_4) {
					openList.Add (adjNode);
				}
			}

			// sort the list based on the f value
			openList = openList.OrderBy (x => x.aStarF).ToList ();
			/*openList.Sort (delegate (PathNode a, PathNode b) {
				return a.aStarF.CompareTo (b.aStarF);
			});*/

			foreach (PathNode currNode in openList) {
				//Console.Out.WriteLine ("findBestPathFromPosToPos currentPos: " + currentPos.x + ", " + currentPos.y + " *-*-* checking currNode.Pos: " + currNode.pos.x + ", " + currNode.pos.y);

				// make sure the newly added nodes are not in the closed list already
				if (closedList.ContainsPathNode (currNode)) {
					//Console.Out.WriteLine ("----- findBestPathFromPosToPos currentPos: " + currentPos.x + ", " + currentPos.y + " *-*-* currNode already closed");
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

					//Console.Out.WriteLine ("----- findBestPathFromPosToPos currentPos: " + currentPos.x + ", " + currentPos.y + " *-*-* currNode chosen with pos: " + currNode.pos.x + ", " + currNode.pos.y + " and direction: " + bestPath[0]);

					return true;
				}
			}

			return false;
		}

		#endregion



		#region Helper Methods

		private double weightHeroInterest (Hero heroInQuestion) {
			// if they don't have at least reasonable percentage of the mines then we're not interested in them
			// unless it's a smaller map then they need at least 2
			int minThreshold = (int)Math.Round ((double)minesPosList.Count * 0.2);
			minThreshold = minThreshold < 2 ? 2 : minThreshold;
			if (heroInQuestion.mineCount < minThreshold)
				return 0;

			// if they're within reach of a tavern ignore them
			Pos currHeroInQuestionPos = heroInQuestion.GetCorrectedHeroPos ();
			foreach (Pos currTavernPos in tavernsPosList) {
				if (currTavernPos.Distance (currHeroInQuestionPos, true) <= 2)
					return 0;
			}

			double gameDonePercent = (double)serverStuff.currentTurn / (double)serverStuff.maxTurns;
			gameDonePercent /= 0.9; // normalize to 90% of the game so we have longer to react if we want to take out a hero late in the game
			double currHeroWeight = 0;
			currHeroWeight += ((double)(heroInQuestion.gold - myHero.gold) / (8 * minesPosList.Count) ) * gameDonePercent;
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

		// finds the closest mine(s) in relation to the bot (myHero)
		// argument amountToFind: amount of mines to compare/find
		// retuns a list of board position(s) of the sorted mine(s) based on distance
		// IMPORTANT: null Pos means there were no mines found (in other words my hero controls them all)
		private List<Pos> findClosestMinePositions (int amountToFind = 1) {
			List <Pos> returnList = new List<Pos> ();

			Tile heroMineTile = Tile.GOLD_MINE_1;
			switch (myHero.id) {
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

			// sort the list of the mines in relation to the current hero's pos
			List <Pos> sortedList = new List<Pos> (minesPosList);
			sortedList.Sort (delegate (Pos a, Pos b) {
				return myHeroPos.Distance(a).CompareTo (myHeroPos.Distance(b));
			});

			for (int i = 0; i < sortedList.Count; ++i) {
				Pos currMinePos = sortedList[i];
				if (tileForCoords (currMinePos.x, currMinePos.y) != heroMineTile) {
					returnList.Add (currMinePos);
				}
				if (returnList.Count == amountToFind)
					break;
			}

			return returnList;
		}

		// finds the closest tavern(s) in relation to the bot (myHero)
		// argument amountToFind: amount of taverns to compare/find
		// retuns a list of board position(s) of the sorted tavern(s) based on distance
		// IMPORTANT: should never be the case but null Pos means there were no taverns found (in other words this map doesn't have any taverns)
		private List<Pos> findClosestTavernPositions (int amountToFind = 1) {
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

		// determines the offset from the bot (myHero) to another tile position
		// -x value is to the left or west
		// -y value is above or to the north
		private Pos offsetToTilePos (Pos tilePos) {
			Pos distPos = new Pos ();
			distPos.x = tilePos.x - myHeroPos.x;
			distPos.y = tilePos.y - myHeroPos.y;

			return distPos;
		}

		// determines the tile for an offset from the bot's (myHero) position
		private Tile tileForOffset (int x, int y)
		{
			x += myHeroPos.x;
			y += myHeroPos.y;

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
