/*
 * ParseMilwaukeeCountyWiVotes.cs
 * 
 * which code and results I will archive at:
 * https://github.com/IneffablePerformativity
 * 
 * "To the extent possible under law, Ineffable Performativity has waived all copyright and related or neighboring rights to
 * The C# program ParseMilwaukeeCountyWiVotes.cs and resultant outputs.
 * This work is published from: United States."
 * 
 * This work is offered as per license: http://creativecommons.org/publicdomain/zero/1.0/
 * 
 * 
 * Goal: Parsing the HTML page from:
 * https://county.milwaukee.gov/EN/County-Clerk/Off-Nav/Election-Results/Election-Results-Fall-2020
 * TITLE: 11-3-20 General and Presidential Election - Unofficial Results
 * 
 * to demonstrate an inverse republicanism::trump relationship
 * as was described in a TGP article at:
 * https://www.thegatewaypundit.com/2020/11/caught-part-3-impossible-ballot-ratio-found-milwaukee-results-change-wisconsin-election-30000-votes-switched-president-trump-biden/
 * 
 * to wit, as suggested in this original hockey-stick scatterplot:
 * https://www.thegatewaypundit.com/wp-content/uploads/2020-Milwaukee-Hockey-Stick-Chart.jpg
 * 
 * 
 * Misc Other URLs:
 * 
 * https://www.nytimes.com/2010/12/11/world/europe/11fraud.html
 * TITLE: Wide Swings in Turnout Viewed as One Sign of Russian Vote Fraud
 * 
 * https://apnews.com/article/election-2020-joe-biden-donald-trump-wisconsin-elections-c14705ea715877b472454e57df022a91
 * TITLE: False claims of Wisconsin voter fraud rely on wrong numbers
 * 
 * https://www.bbc.com/news/election-us-2020-54811410
 * TITLE: US election 2020: Five viral vote claims fact-checked
 * 
 * 
 * Manually viewing the saved page in Firefox to inspect HTML elements, I note:
 * Last Updated: Nov. 4, 2020 3:56 a.m.
 * Total number of wards:478
 * Ballots cast:460300
 * 
 * NuGet is no good in Sharp Develop;
 * I keep copying around an HtmlAgilityPack.dll from 2017-11-31 to reference.
 * 
 */


using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using HtmlAgilityPack;

using System.Drawing; // Must add a reference to System.Drawing.dll
using System.Drawing.Imaging; // for ImageFormat


namespace ParseMilwaukeeCountyWiVotes
{
	
	// Class to describe one Candidate in a Race.
	
	class Candidate
	{
		public string CandidateName = "";
		public bool isDemocrat = false;
		public bool isRepublican = false;
		public bool isSomeOther = false;
		public int totalVotesInRace = 0;
		public Dictionary<int, int> WardsVotes = new Dictionary<int, int>();
	}
	
	// Class to describe one Race in Milwaukee County, Wisconsin.
	
	class Race
	{
		public int RaceNumber = 0; // ordinal as appears in HTML page
		public string RaceName = "";
		public bool isPOTUS = false;
		// It appears there is no federal level SENUS in WI this year
		public bool isREPUS = false; // federal level
		public bool isSenWi = false; // state level
		public bool isRepWi = false; // state level
		public bool isMinor = false;
		public int QtyCandidates = 0;
		public Candidate[] Candidates = new Candidate[0]; // re-init later
		public bool hasDemocrat = false;
		public bool hasRepublican = false;
		public bool hasSomeOther = false;
		public List<int> WardNumbersInRace = new List<int>();
	}
	
	class Program
	{
		// Input file is as manually saved from Firefox on 2020-11-15:
		static string inputFilepath = @"C:\A\SharpDevelop\ParseMilwaukeeCountyWiVotes\Milwaukee County - 11-3-20 General and Presidential Election - Unofficial Results.html";

		static string DateTimeStr = DateTime.Now.ToString("yyyyMMddTHHmmss");
		
		// Outputs exploratory, debug data:
		static string logFilePath = @"C:\A\" + DateTimeStr + "_ParseMilwaukeeCountyWiVotes_log.txt";
		static TextWriter twLog = null;
		
		// Eventually, this will document what gets plotted:
		// I can race over to Excel to see some quick plots.
		// E.g., One might sort by some column,
		// delete some of the too many columns,
		// Transpose, swap rows and cols, thus:
		// (Copy cell range; Paste to a new area, Choose Paste...Transpose)
		// insert a recommended line graph type,
		// uncheck some of the too many series.
		static string csvFilePath = @"C:\A\" + DateTimeStr + "_ParseMilwaukeeCountyWiVotes_csv.csv";
		static TextWriter twCsv = null;
		
		// Output my own bitmap for fine grained control of data display:
		static string pngFilePath = @"C:\A\" + DateTimeStr + "_ParseMilwaukeeCountyWiVotes_png.png";
		static bool doDrawGraticulesBetweenEachWard = true; // may look better when false
		static bool doPlotSubracesWithinWards = false; // may look better when false
		
		// a favorite output idiom
		static void say(string msg)
		{
			twLog.WriteLine(msg);
			// Console.WriteLine(msg);
		}
		
		static void csv(string msg)
		{
			twCsv.WriteLine(msg);
		}

		
		// Debugs, but TMI during development, but now to archive:
		static bool doDumpAllTablesFirst = true;
		static bool debugBreakAfterOneRace = false;

		
		// According to Foreknowledge of this specific web page already saved:
		// --
		// -- exhibited range of ward numbers is 1 to 478
		const int minWardNo = 1;
		const int maxWardNo = 478;
		// --
		// -- total ballots cast was stated
		const int BallotsCast = 460300;
		// --
		// -- valid indexing[1...n] range of races in the 72 total HTML tables:
		const int minRaceNo = 1;
		static int maxRaceNo = debugBreakAfterOneRace ? 1 : 35;


		// Variables for the first phase: Inputting HTML web page:
		
		// This array will hold all Race instances where [race# == HTML table# / 2]:
		static Race[] races = new Race[1 + maxRaceNo]; // index as 1 up.

		// Regex solves before and after " (" in such strings as "WRITE-IN (Nonpartisan)"
		static Regex reCandidateParty = new Regex(@"^(?<candidate>.*) \((?<party>.*)\)$", RegexOptions.Compiled);
		
		// Many HTML innertext fields contain or end with one utf-8 C2 A0. Rid:
		static char [] caSpaceHighSpace = {' ', '\u00A0'};

		
		// Variables for the second phase: Analysis derived data:
		
		// I will convert the 35 races into more granular 478 wards view.

		// Class to describe one Ward.
		
		class Ward
		{
			public int WardNumber = 0;
			public string WardName = "";

			public int TotalRegisteredInWard = 0;
			public int TotalBallotsCastInWard = 0;
			
			// Each of 31 races may appear in some or all of 478 wards.
			// Each ward participates in about 7 - 8 races:
			// As per intel far below, every ward votes in:
			// 1 POTUS
			// 1 REPUS
			// 1 SenWi (some opposed, some unopposed)
			// 1 RepWi (some opposed, some unopposed)
			// up to 4 minor races, all unopposed
			public List<int> RaceNumbersInWard = new List<int>();

			// Store the votes cast in each ward by race number [1..n].
			public Dictionary<int, int> RacesDemVotes = new Dictionary<int, int>();
			public Dictionary<int, int> RacesRepVotes = new Dictionary<int, int>();

			// Percentages (X/TotalBallotsPerWard) scaled to parts-per-million:
			public int ppmDemPOTUS = 0;
			public int ppmRepPOTUS = 0;
			public int ppmDemREPUS = 0;
			public int ppmRepREPUS = 0;
			public int ppmDemSenWi = 0;
			public int ppmRepSenWi = 0;
			public int ppmDemRepWi = 0;
			public int ppmRepRepWi = 0;
			public int ppmDemMinor = 0; // highest of up to 4 minor races
			public int ppmRepMinor = 0;
			
			// these serve an early theory to measure "republicanism":
			// I took the maximum percentage of all non-POTUS races.
			public int ppmDemMaxes = 0; // highest of non-POTUS ppm above
			public int ppmRepMaxes = 0;

			// these serve another theory to measure "republicanism":
			// Perhaps a ratio of Dem to Rep Maxes should order plot.
			// However, let's tweak to a zero-symmetrical value, per:
			// natural log(p/(1−p)) being called the logit function
			// where probability p remains a ratio Rep / (Rep + Dem).
			public int logitMaxes = 0; // this one may be negative

			// these serve my latest theory to measure "republicanism":
			// Take the average of only CONTESTED (REPUS, SenWi, RepWi),
			// as I observe that uncontested races seem to spike higher.
			public int ppmDemAverage = 0;
			public int ppmRepAverage = 0;
			public int logitAverage = 0; // this one may be negative
			
			// I have read that very high (~90%?) voter turnout may be a sign of fraud.
			public int ppmVoterTurnout = 0; // another ppm, maybe for plot ordering

			// I will plot each ward with a bar width proportional to votes cast.
			public int wardBarPixels = 0;
		}
		
		// Store them all here:
		static Ward[] wards = new Ward[1 + maxWardNo]; // index as 1 up.
		
		
		public static void Main(string[] args)
		{
			Console.WriteLine("Hello World!");
			
			// TODO: Implement Functionality Here

			using(twLog = File.CreateText(logFilePath))
				using(twCsv = File.CreateText(csvFilePath))
			{
				csv("WardNo,Registered,TotalBallots,ppmVoterTurnout,ppmDemPOTUS,ppmRepPOTUS,ppmDemAverage,ppmRepAverage,logitAverage,ppmDemMaxes,ppmRepMaxes,logitMaxes,ppmDemREPUS,ppmRepREPUS,ppmDemSenWi,ppmRepSenWi,ppmDemRepWi,ppmRepRepWi,ppmDemMinor,ppmRepMinor,WardName");

				try { doit(); } catch(Exception e) {say(e.ToString());}
			}
			
			Console.Write("Press any key to continue . . . ");
			Console.ReadKey(true);
		}
		
		static void doit()
		{
			// Phase one - inputting HTML data
			{
				HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
				doc.Load(inputFilepath, Encoding.UTF8); // Firefox saved as UTF8 w/o BOM
				
				// Let's first find this table and extract the total vote counts:
				
				//Ward 	Registered Voters - Total 	Ballots Cast - Total
				//1 	City of Milwaukee Ward 1  	1502 	1185
				//2 	City of Milwaukee Ward 2  	524 	358
				//...
				//478 	V. Whitefish Bay Ward 12  	1068 	998
				//Total 	550132 	460300

				// Looks like all 36 tables can be found thus:
				// <table cellspacing="0" class="precinctTable">
				//
				// Wait, that's every 2nd table; no text therein mentions republican.
				// It's alternating PRIOR <table cellspacing="0""> has such factoids!

				// Process all HTML tables found:
				HtmlNodeCollection tableCollection = doc.DocumentNode.SelectNodes("( //table )");
				if (tableCollection != null)
				{
					// optional baby step / debug feature:
					if(doDumpAllTablesFirst)
					{
						// This simple code will print out ALL table contents:
						int tableNo = 0;
						foreach (HtmlNode tableNode in tableCollection)
						{
							tableNo++;
							string tableNoStr = tableNo.ToString().PadLeft(4);
							say("TABLE #" + tableNoStr);
							// headers and data are all carried as <tr>, print all for now:
							HtmlNodeCollection tableRowCollection = tableNode.SelectNodes(".//tr");
							if(tableRowCollection != null)
							{
								int rowNo = 0;
								foreach (HtmlNode tableRow in tableRowCollection)
								{
									rowNo++;
									string rowNoStr = rowNo.ToString().PadLeft(4);
									say("ROW #" + rowNoStr);
									HtmlNodeCollection tableCellCollection = tableRow.SelectNodes(".//td");
									if(tableCellCollection != null)
									{
										int colNo = 0;
										foreach (HtmlNode tableCell in tableCellCollection)
										{
											colNo++;
											string colNoStr = colNo.ToString().PadLeft(4);
											string trim = tableCell.InnerText.Trim(caSpaceHighSpace);
											say("["+tableNoStr+","+rowNoStr+","+colNoStr+"]: [" + trim + "]");
										}
									}
								}
							}
							if(debugBreakAfterOneRace
							   && tableNo == 4) // 1,2 is xxx, 3 is POTUS header, 4 is POTUS votes
								break;
						}
					}

					
					// Continue working with the already gathered tableCollection:

					
					// Next, Extract table[1] of all wards Numbers and total ballots cast:
					{
						HtmlNode tableNode = tableCollection[1];
						HtmlNodeCollection tableRowCollection = tableNode.SelectNodes(".//tr");
						int sumOfBallots = 0;
						for(int i = minWardNo; i <= maxWardNo; i++)
						{
							// row 0 was a header, rest contain ward no == index no:
							HtmlNode tableRow = tableRowCollection[i];
							HtmlNodeCollection tableCellCollection = tableRow.SelectNodes(".//td");

							// every row has 4 cells #0=ward, #1=name #2=registered #3=ballots cast
							int wardNumber = int.Parse(tableCellCollection[0].InnerText.Trim(caSpaceHighSpace));
							string wardName = tableCellCollection[1].InnerText.Trim(caSpaceHighSpace);
							int Registered = int.Parse(tableCellCollection[2].InnerText.Trim(caSpaceHighSpace));
							int totalBallots = int.Parse(tableCellCollection[3].InnerText.Trim(caSpaceHighSpace));

							sumOfBallots += totalBallots;
							if(wardNumber != i)
								throw(new Exception("wardNumber != i"));
							
							// prepare a little list building for use a lot later:
							wards[wardNumber] = new Ward();
							wards[wardNumber].WardNumber = wardNumber;
							wards[wardNumber].WardName = wardName;
							wards[wardNumber].TotalRegisteredInWard = Registered;
							wards[wardNumber].TotalBallotsCastInWard = totalBallots;
						}

						if(sumOfBallots != BallotsCast)
							throw(new Exception("sumOfBallots != BallotsCast"));
						// Okay, that loop ran to perfection...
					}

					

					// Next, extract EVEN table[2,4,6,...,70] of identifiers incl republican etc.
					// For example, table[2] describes the POTUS = PRESIDENTIAL race!

					
					// Now, how shall I organize such data?
					// 1. Recognize rows containing "(Democratic)" and "(Republican)" versus all other rows.
					// 2. strip out the strings prior to (...) to verify the parse in alternate tables[3,5,...]
					// 3. Use race's count of rows to set the qty of cells per row in alternate tables[3,5,...]
					// 4. Recognize these major categories of race names, containing:
					// -- President Vice President
					// -- Representative in Congress
					// -- State Senator (that is, WITHIN the state, not a US Senator)
					// -- Representative to the Assembly
					// -- Anything else, which log to prove nothing major was missed.
					// 5. Recognize races according to whether they have partisan data:
					// -- Recognize races that both one D candidate and one R candidate
					// -- Recognize races that have just one (R or D) candidate
					// -- Anything else, which log to prove nothing major was missed.

					
					// And how shall I go forth with such data?
					// Create some race object for each race.
					// hold them in some array races[tableIndex/2].
					// Create some candidate sub-object for each candidate.
					//
					// Okay, I've done that much; Is this plan still good:
					// Create some list of wardNumbers that voted in race.
					// Create some list of votes earned[wardNumber x candidate].
					//
					// Keep some global top summary collection by ward number.
					// If (ward) voted on (race), hang that race under top[ward].
					// A later analysis phase will iterate top[ward] to ponder ratios.
					// Also later analysis phase might iterate races to ponder ratios.

					
					// I thought to display a lines-over-bars graph,
					// possibly ordering by {population or partisanity},
					// as tiny wards should crowd to the end of a graph;
					// but most wards have about .5K to 2K ballots cast.
					// I read that high Cast/Registered may imply fraud!
					
					// Partisanity could be ratio Rep/Dem in any of:
					// -- Presidential Race
					// -- US Congress Race(s)
					// -- State Senate Race(s)
					// -- State RepWi Races(s)
					// -- Minor Races(s)
					// -- Max [D,R] votes across all or some races.
					// -- A. only contested, both D and R
					// -- B. only uncontested, only one D or R
					// -- C. but ignore races where neither D nor R.
					
					// For each ward, make an Vertical BAR Stripe with
					// -- Dem % of total votes descending from top,
					// -- Rep % of total votes rising from bottom.
					//
					// Stripe width = 1+n; n proportional to cast votes.
					// Sub-stripes if more than one race/ward displayed.
					//
					// 2 varying lines plotting D% and R% presidential race.
					// 1? Pixel black horiz raster between each ward stripe.
					// 1? Pixel vertical black graticules at 10%
					
					
					int nRepublicans = 0;
					int nDemocrats = 0;
					int nSomeOther = 0;
					for(int tableIndex = 2; tableIndex <= 70; tableIndex += 2)
					{
						int RaceNumber = tableIndex/2;
						Race thisRace = new Race();
						thisRace.RaceNumber = RaceNumber; // baby step
						races[RaceNumber] = thisRace;
						
						HtmlNode tableNode = tableCollection[tableIndex];
						HtmlNodeCollection tableRowCollection = tableNode.SelectNodes(".//tr");
						string raceName = tableRowCollection[0].SelectNodes(".//td")[0].InnerText.Trim(caSpaceHighSpace);
						say("RACE NAME = [" + raceName + "]");
						thisRace.RaceName = raceName;
						// -- President Vice President
						// -- Representative in Congress
						// -- State Senator
						// -- Representative to the Assembly
						if(raceName.Contains("President Vice President"))
						{
							thisRace.isPOTUS = true;
						}
						else if(raceName.Contains("Representative in Congress"))
						{
							thisRace.isREPUS = true;
						}
						else if(raceName.Contains("State Senator"))
						{
							thisRace.isSenWi = true;
						}
						else if(raceName.Contains("Representative to the Assembly"))
						{
							thisRace.isRepWi = true;
						}
						else
						{
							thisRace.isMinor = true;
							say("thisRace.isMinor: " + thisRace.RaceName);
						}
						
						thisRace.QtyCandidates = tableRowCollection.Count - 1;
						thisRace.Candidates = new Candidate[1 + thisRace.QtyCandidates]; // index 1 up

						// Rows[1,...N-1] each describe one candidate (or WRITE-IN) in race.
						
						for(int i = 1; i < tableRowCollection.Count; i++)
						{
							Candidate thisCandidate = new Candidate();
							thisRace.Candidates[i] = thisCandidate;
							
							// row 0 is a header, rows [1...n-1] describe the candidates
							HtmlNode tableRow = tableRowCollection[i];
							HtmlNodeCollection tableCellCollection = tableRow.SelectNodes(".//td");
							
							// every row has 4 cells #0=candidateField, #3=total votes cast in race
							int totalVotesInRace = int.Parse(tableCellCollection[3].InnerText.Trim(caSpaceHighSpace));
							thisCandidate.totalVotesInRace = totalVotesInRace;
							
							string candidateField = tableCellCollection[0].InnerText.Trim(caSpaceHighSpace);
							say("CANDIDATE FIELD = [" + candidateField + "] got [" + totalVotesInRace + "] totalVotesInRace");

							// What a nice, regular web page: Every row matches this format:
							Match m = reCandidateParty.Match(candidateField);
							if(m.Success == false)
							{
								throw(new Exception("reCandidateParty.MatchSuccess == false: " + candidateField));
							}
							thisCandidate.CandidateName = m.Groups["candidate"].ToString();
							
							string partyName = m.Groups["party"].ToString(); // Huh, how not so? Oh, regex typo.
							switch(partyName)
							{
								case "Democratic": // Hmmm. Trump says NOT ...ic!
									thisCandidate.isDemocrat = true;
									thisRace.hasDemocrat = true;
									nDemocrats ++;
									break;
								case "Republican":
									thisCandidate.isRepublican = true;
									thisRace.hasRepublican = true;
									nRepublicans ++;
									break;
								default:
									thisCandidate.isSomeOther = true;
									thisRace.hasSomeOther = true;
									nSomeOther ++;
									say("thisCandidate.isSomeOther [" + partyName+ "]: " + candidateField);
									break;
							}
						}
						if(debugBreakAfterOneRace)
							break;
					}
					
					// having looped over all the even tables, show me sanity:
					say("nDemocrats = " + nDemocrats);
					say("nRepublicans = " + nRepublicans);
					say("nSomeOther = " + nSomeOther);


					// Next, Extract table[3,5,7,...,71] of vote counts / ward.
					// For example, table[2] describes the PRESIDENTIAL race!
					
					
					// So in these every other ODD alternate tables:
					// col[0] = ward no
					// col[1] = ward name
					// cols[2..n-1] = votes/candidate (indexed aboutlike prior table)
					// except that in top row, these contain candidate name atop column

					for(int tableIndex = 3; tableIndex <= 71; tableIndex += 2)
					{
						int RaceNumber = tableIndex/2;
						Race thisRace = races[RaceNumber]; // created in prior EVENS loop
						
						HtmlNode tableNode = tableCollection[tableIndex];
						HtmlNodeCollection tableRowCollection = tableNode.SelectNodes(".//tr");
						
						// first, verify all names [2..n-1] match in the top row:

						{
							HtmlNodeCollection  topRowCells = tableRowCollection[0].SelectNodes(".//td");
							for(int i = 1; i <= thisRace.QtyCandidates; i++)
							{
								int skip2Columns = i + 1;
								string candyName = topRowCells[skip2Columns].InnerText.Trim(caSpaceHighSpace);
								say("CANDY NAME = [" + candyName + "]");
								if(candyName != thisRace.Candidates[i].CandidateName)
								{
									throw(new Exception("candyName != thisRace.Candidates[i].CandidateName"));
								}
								// and create the dict; No, static ctor did it okay.
							}
						}

						
						// second, run down all the ward rows of table:
						
						// wrong: for(int j = 1; j <= maxWardNo; j++)
						// Wards do not contain ALL POSSIBLE races!

						int nWardRows = tableRowCollection.Count - 2; // Skip [0] AND LAST ROW!
						for(int j = 1; j <= nWardRows; j++)
						{
							HtmlNodeCollection  wardRowCells = tableRowCollection[j].SelectNodes(".//td");
							
							// treat column 0

							int wardNumber = int.Parse(wardRowCells[0].InnerText.Trim(caSpaceHighSpace));
							thisRace.WardNumbersInRace.Add(wardNumber);
							
							// skip col 1 = ward name.
							
							// treat candidate vote columns in this one ward
							for(int i = 1; i <= thisRace.QtyCandidates; i++)
							{
								int skip2Columns = i + 1;
								int votes = int.Parse(wardRowCells[skip2Columns].InnerText.Trim(caSpaceHighSpace));
								// again, we are treating votes for thisRace.Candidates[i].
								// Do what with them?  Wow: It was this easy!
								thisRace.Candidates[i].WardsVotes.Add(wardNumber, votes);
							}
						}

						
						// having input all the votes for this race,
						// verify the sum up matches expected total.

						
						for(int i = 1; i <= thisRace.QtyCandidates; i++)
						{
							int expected = thisRace.Candidates[i].totalVotesInRace;
							int sumVotes = 0;
							foreach(int votes in thisRace.Candidates[i].WardsVotes.Values)
							{
								sumVotes += votes;
							}
							if(sumVotes != expected)
							{
								throw(new Exception("sumVotes != expected"));
							}
						}

						if(debugBreakAfterOneRace)
							break;
					}
				}
			}

			// There ends the inputting of HTML table data.

			
			// Phase two -- ANALYSIS! Nah, maybe in the next loop.

			
			// A few more debugs:
			if(doDumpAllTablesFirst)
			{
				// Start verifing the structures being created.
				// Later, this loop will develop into analysis.
				int nPOTUS = 0;
				int nREPUS = 0;
				int nSenWi = 0;
				int nRepWi = 0;
				int nMinor = 0;
				int nBothDemRep = 0;
				int nOneDemOrRep = 0;
				int nOneDem = 0;
				int nOneRep = 0;
				int nNoDemNorRep = 0;
				for(int i = minRaceNo; i <= maxRaceNo; i++)
				{
					Race thisRace = races[i];
					if(thisRace.isPOTUS)
						nPOTUS ++;
					if(thisRace.isREPUS)
						nREPUS ++;
					if(thisRace.isSenWi)
						nSenWi ++;
					if(thisRace.isRepWi)
						nRepWi ++;
					if(thisRace.isMinor)
						nMinor ++;
					if(thisRace.hasDemocrat)
					{
						if(thisRace.hasRepublican)
						{
							nBothDemRep ++;
						}
						else
						{
							nOneDemOrRep ++;
							nOneDem ++;
						}
					}
					else
					{
						if(thisRace.hasRepublican)
						{
							nOneDemOrRep ++;
							nOneRep ++;
						}
						else
						{
							nNoDemNorRep ++;
						}
					}
				}

				int nRaces = nPOTUS + nREPUS + nSenWi + nRepWi + nMinor;
				say("nPOTUS = " + nPOTUS);
				say("nREPUS = " + nREPUS);
				say("nSenWi = " + nSenWi);
				say("nRepWi = " + nRepWi);
				say("nMinor = " + nMinor);
				say("nRaces (s/b 35) = " + nRaces); // should be 35
				say("nRacesBothDemRep = " + nBothDemRep);
				say("nRacesOneDemOrRep = " + nOneDemOrRep);
				say("nRacesOneDem = " + nOneDem);
				say("nRacesOneRep = " + nOneRep);
				say("nRacesNoDemNorRep = " + nNoDemNorRep);
			}

			
			// I am getting HOT to analyze and plot!
			
			
			// This code block converts the ~30 races
			// into a "more granular" ~500 wards view:

			{
				for(int i = minRaceNo; i <= maxRaceNo; i++)
				{
					Race thisRace = races[i];
					foreach(int wardNumber in thisRace.WardNumbersInRace)
					{
						wards[wardNumber].RaceNumbersInWard.Add(thisRace.RaceNumber);

						// Convert each race * candidate[ward] votes into ward[D,R][race] votes:

						for(int j = 1; j <= thisRace.QtyCandidates; j++)
						{
							if(thisRace.Candidates[j].isDemocrat)
							{
								wards[wardNumber].RacesDemVotes.Add(thisRace.RaceNumber, thisRace.Candidates[j].WardsVotes[wardNumber]);
							}
							if(thisRace.Candidates[j].isRepublican)
							{
								wards[wardNumber].RacesRepVotes.Add(thisRace.RaceNumber, thisRace.Candidates[j].WardsVotes[wardNumber]);
							}
						}
					}
				}
			}
			

			// more code development research:
			//{
			//	// be sure of the spread of counts; to skip almost empty wards:
			//	const int maxBallots = 9000; // gross guess, eyeballed
			//	int[] histogram = new int[maxBallots];
			//	foreach(int tv in TotalBallotsPerWard)
			//		histogram[tv]++;
			//	for(int i = 0; i < maxBallots; i++)
			//	{
			//		if(histogram[i] > 0)
			//			say("HISTOGRAM[" + i.ToString().PadLeft(3) + "] = " + histogram[i]);
			//	}
			//	// result:
			//	//HISTOGRAM[  0] = 4
			//	//HISTOGRAM[  1] = 1
			//	//HISTOGRAM[  5] = 1
			//	//HISTOGRAM[ 41] = 1
			//	//HISTOGRAM[ 72] = 1
			//	//HISTOGRAM[111] = 1
			//	//...
			//	//HISTOGRAM[3533] = 1
			//	//HISTOGRAM[3670] = 1
			//	//HISTOGRAM[3770] = 1
			//}
			
			// Keep a highwater for eight, or ten, intels
			
			// 1.Unopposed:
			int maxREPUS1 = 0;
			int maxSenWi1 = 0;
			int maxRepWi1 = 0;
			int maxMinor1 = 0;

			// 2.Opposed:
			int maxREPUS2 = 0;
			int maxSenWi2 = 0;
			int maxRepWi2 = 0;
			int maxMinor2 = 0;

			// 3. Finally-Anally
			int maxSenWi = 0;
			int maxRepWi = 0;
			
			// baby sanity step
			int nWardsActive = 0;

			for(int i = minWardNo; i <= maxWardNo; i++)
			{
				// good. 7-8 typ....
				say("Ward " + i + " held " + wards[i].RaceNumbersInWard.Count + " races.");
				
				// now iterate rep and dem lists in ward:
				Ward thisWard = wards[i];

				// Per Histogram Research, Skip a few tiny tiny wards.
				if(thisWard.TotalBallotsCastInWard < 40)
					continue;
				
				nWardsActive ++;
				
				// gimme intel on range of qty races per type:
				// I know there is EXACTLY one POTUS race/ward.

				// 1.Unopposed:
				int nREPUS1 = 0;
				int nSenWi1 = 0;
				int nRepWi1 = 0;
				int nMinor1 = 0;

				// 2.Opposed:
				int nREPUS2 = 0;
				int nSenWi2 = 0;
				int nRepWi2 = 0;
				int nMinor2 = 0;

				// My latest ward ordering heuristic:
				int sumToAvgDem = 0;
				int qtyToAvgDem = 0;
				int sumToAvgRep = 0;
				int qtyToAvgRep = 0;
				
				foreach(int j in thisWard.RaceNumbersInWard)
				{
					// very possibly (yes, exception proves that)
					// some D|R have 0 votes, did not get added.

					int nDem = 0;
					if(thisWard.RacesDemVotes.ContainsKey(j))
						nDem = thisWard.RacesDemVotes[j];
					
					int nRep = 0;
					if(thisWard.RacesRepVotes.ContainsKey(j))
						nRep = thisWard.RacesRepVotes[j];
					
					// Discard any races with NEITHER D NOR R:

					if(nDem == 0 && nRep == 0)
						continue;

					// (ASSUME) zero votes means unopposed
					if(nDem == 0 || nRep == 0)
					{
						// get intel regarding races[j] in this ward:
						if(races[j].isREPUS)
							nREPUS1 ++;
						if(races[j].isSenWi)
							nSenWi1 ++;
						if(races[j].isRepWi)
							nRepWi1 ++;
						if(races[j].isMinor)
							nMinor1 ++;
					}
					else
					{
						// These are the CONTESTED races:
						// Plus Obviously, POTUS race too, everywhere.
						if(races[j].isREPUS)
							nREPUS2 ++;
						if(races[j].isSenWi)
							nSenWi2 ++;
						if(races[j].isRepWi)
							nRepWi2 ++;
						if(races[j].isMinor)
							nMinor2 ++; // no contested minor races were found
					}
					
					// this made a nice baby step debug, but must dice data finer:
					//say("Ward " + i.ToString().PadLeft(3) + " Race " + j.ToString().PadLeft(2) +
					//    ": Dems " + nDem.ToString().PadLeft(6) +
					//    ", Reps " + nRep.ToString().PadLeft(6));
					
					{
						// Resume per-ward Computations, using these
						// Conclusions from intel below:
						// Every ward can vote in:
						// 1 POTUS
						// 1 US REP
						// 1 Wi SEN (some opposed, some unopposed)
						// 1 Wi REP (some opposed, some unopposed)
						// up to 4 minor races, ALL WERE UNOPPOSED
						
						// So, being careful not to divide by zero,
						// for each ward, compute:
						// -- not %(D/R), but %(D/Total Ballots), %(R/Total Ballots), of:
						// - 1. one POTUS pair
						// - 2. one USREP pair
						// - 3. one WiSen pair
						// - 4. one WiRep pair
						// - 5. one Minor pair (take the max of 0-4 races)
						// - 6. one MAXes pair (over prior [2..5] down-races)

						// Solve various ward ordering rankings based on:
						// - Descending population (figure LARGE ~= URBAN ~= DEMOC.);
						// - Descending democraticity (choice of [2..6] ratios)
						//   (--BECAUSE ORIGINAL SCATTERPLOT HAD AS ITS X AXIS,
						//   SOME KIND OF METRIC THEY CALLED ~REPUBLICANICITY);
						
						// I detest floats, so scale percentage * 1,000,000 into PPM.

						// hence, make new ward members:
						//int ppmDemPOTUS = 0;
						//int ppmRepPOTUS = 0;
						//int ppmDemREPUS = 0;
						//int ppmRepREPUS = 0;
						//int ppmDemSenWi = 0;
						//int ppmRepSenWi = 0;
						//int ppmDemRepWi = 0;
						//int ppmRepRepWi = 0;
						//int ppmDemMinor = 0; // highest of up to 4 minor races
						//int ppmRepMinor = 0;
						//int ppmDemMaxes = 0; // highest of non-POTUS ppm above
						//int ppmRepMaxes = 0;

						int wtb = thisWard.TotalBallotsCastInWard;
						
						// compute the per-race ppm:
						int ppmDem = (int)(nDem * 1000000L / wtb);
						int ppmRep = (int)(nRep * 1000000L / wtb);

						// assign to some ward ppm member:
						if(races[j].isPOTUS)
						{
							thisWard.ppmDemPOTUS = ppmDem;
							thisWard.ppmRepPOTUS = ppmRep;
						}
						if(races[j].isREPUS)
						{
							thisWard.ppmDemREPUS = ppmDem;
							thisWard.ppmRepREPUS = ppmRep;
							if(nDem > 0 && nRep > 0)
							{
								// My latest ward ordering heuristic:
								sumToAvgDem += ppmDem;
								qtyToAvgDem ++;
								sumToAvgRep += ppmRep;
								qtyToAvgRep ++;
							}
						}
						if(races[j].isSenWi)
						{
							thisWard.ppmDemSenWi = ppmDem;
							thisWard.ppmRepSenWi = ppmRep;
							if(nDem > 0 && nRep > 0)
							{
								// My latest ward ordering heuristic:
								sumToAvgDem += ppmDem;
								qtyToAvgDem ++;
								sumToAvgRep += ppmRep;
								qtyToAvgRep ++;
							}
						}
						if(races[j].isRepWi)
						{
							thisWard.ppmDemRepWi = ppmDem;
							thisWard.ppmRepRepWi = ppmRep;
							if(nDem > 0 && nRep > 0)
							{
								// My latest ward ordering heuristic:
								sumToAvgDem += ppmDem;
								qtyToAvgDem ++;
								sumToAvgRep += ppmRep;
								qtyToAvgRep ++;
							}
						}
						if(races[j].isMinor)
						{
							// Omit these Minor races from the Maxes.
							if(thisWard.ppmDemMinor < ppmDem)
								thisWard.ppmDemMinor = ppmDem;
							if(thisWard.ppmRepMinor < ppmRep)
								thisWard.ppmRepMinor = ppmRep;
						}
					}
				}
				
				// My latest ward ordering heuristic:
				// Compute my new measure of republicanism:

				thisWard.ppmDemAverage = sumToAvgDem / qtyToAvgDem;
				thisWard.ppmRepAverage = sumToAvgRep / qtyToAvgRep;

				
				// Having done all races in ward, get the Maxes of non-POTUS

				
				thisWard.ppmDemMaxes = thisWard.ppmDemREPUS;

				if(thisWard.ppmDemMaxes < thisWard.ppmDemSenWi)
					thisWard.ppmDemMaxes = thisWard.ppmDemSenWi;

				if(thisWard.ppmDemMaxes < thisWard.ppmDemRepWi)
					thisWard.ppmDemMaxes = thisWard.ppmDemRepWi;

				if(thisWard.ppmDemMaxes < thisWard.ppmDemMinor)
					thisWard.ppmDemMaxes = thisWard.ppmDemMinor;

				
				thisWard.ppmRepMaxes = thisWard.ppmRepREPUS;

				if(thisWard.ppmRepMaxes < thisWard.ppmRepSenWi)
					thisWard.ppmRepMaxes = thisWard.ppmRepSenWi;

				if(thisWard.ppmRepMaxes < thisWard.ppmRepRepWi)
					thisWard.ppmRepMaxes = thisWard.ppmRepRepWi;

				if(thisWard.ppmRepMaxes < thisWard.ppmRepMinor)
					thisWard.ppmRepMaxes = thisWard.ppmRepMinor;


				// This alternative is obsolete, not as smooth:
				// Now compute a Republicanism ordering metric:
				// zeros in both Dem, Rep having been excluded.
				{
					// step one is to compute a fraction [0..1].
					double fraction = (double) thisWard.ppmRepMaxes /
						(thisWard.ppmDemMaxes + thisWard.ppmRepMaxes);
					// step two is to compute the logit function.
					double odds = fraction / (1 - fraction);
					// Then scale output to what integer range?
					// I see Dem has some 98.xxx percent (D/(D+R)).
					// Thus suppose min Rep is > about 0.01
					// then odds = .01 / .99 or say .99 / .01 == 99
					// ln(99) = 4.595... Suppose 20 is very outside.
					// Scale up such a 2 digits to 6 digits (+sign).
					// Oh wait, I sorted as text. Shift up to [1-999999].
					thisWard.logitMaxes = (int)(Math.Log(odds) * 10000 + 500000);
					if(thisWard.logitMaxes > 999999)
						thisWard.logitMaxes = 999999;
					if(thisWard.logitMaxes < 1)
						thisWard.logitMaxes = 1;
				}

				// My latest ward ordering heuristic:
				// repeat logit computation for Averages
				{
					// step one is to compute a fraction [0..1].
					double fraction = (double) thisWard.ppmRepAverage /
						(thisWard.ppmDemAverage + thisWard.ppmRepAverage);
					// step two is to compute the logit function.
					double odds = fraction / (1 - fraction);
					// Then scale output to what integer range?
					// I see Dem has some 98.xxx percent (D/(D+R)).
					// Thus suppose min Rep is > about 0.01
					// then odds = .01 / .99 or say .99 / .01 == 99
					// ln(99) = 4.595... Suppose 20 is very outside.
					// Scale up such a 2 digits to 6 digits (+sign).
					// Oh wait, I sorted as text. Shift up to [1-999999].
					thisWard.logitAverage = (int)(Math.Log(odds) * 10000 + 500000);
					if(thisWard.logitAverage > 999999)
						thisWard.logitAverage = 999999;
					if(thisWard.logitAverage < 1)
						thisWard.logitAverage = 1;
				}
				
				// compute some more alternative orderings, such as voter turnout:
				// Also, high (like 90%) voter turnout may be a fraud indication.
				
				thisWard.ppmVoterTurnout = (int)(
					1000000L * thisWard.TotalBallotsCastInWard / thisWard.TotalRegisteredInWard
				);
				

				// time for a toddler step debug:
				// or make it a real deliverable:

				{
					StringBuilder sb = new StringBuilder();
					sb.Append(thisWard.WardNumber.ToString().PadLeft(4));
					sb.Append(',');
					sb.Append(thisWard.TotalRegisteredInWard.ToString().PadLeft(4));
					sb.Append(',');
					sb.Append(thisWard.TotalBallotsCastInWard.ToString().PadLeft(4));
					sb.Append(',');
					sb.Append(thisWard.ppmVoterTurnout.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.ppmDemPOTUS.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.ppmRepPOTUS.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.ppmDemAverage.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.ppmRepAverage.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.logitAverage.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.ppmDemMaxes.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.ppmRepMaxes.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.logitMaxes.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.ppmDemREPUS.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.ppmRepREPUS.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.ppmDemSenWi.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.ppmRepSenWi.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.ppmDemRepWi.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.ppmRepRepWi.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.ppmDemMinor.ToString().PadLeft(7));
					sb.Append(',');
					sb.Append(thisWard.ppmRepMinor.ToString().PadLeft(7));
					sb.Append(',');
					// Put this variable width item last:
					sb.Append(thisWard.WardName.Replace(",", "")); // rid any commas

					csv(sb.ToString());
				}

				// update highwater for eight, or ten, intels

				// 1.Unopposed:
				if(maxREPUS1 < nREPUS1)
					maxREPUS1 = nREPUS1;
				if(maxSenWi1 < nSenWi1)
					maxSenWi1 = nSenWi1;
				if(maxRepWi1 < nRepWi1)
					maxRepWi1 = nRepWi1;
				if(maxMinor1 < nMinor1)
					maxMinor1 = nMinor1;

				// 2.Opposed:
				if(maxREPUS2 < nREPUS2)
					maxREPUS2 = nREPUS2;
				if(maxSenWi2 < nSenWi2)
					maxSenWi2 = nSenWi2;
				if(maxRepWi2 < nRepWi2)
					maxRepWi2 = nRepWi2;
				if(maxMinor2 < nMinor2)
					maxMinor2 = nMinor2;

				// 3.Finally-anally
				if(maxSenWi < nSenWi1 + nSenWi2)
					maxSenWi = nSenWi1 + nSenWi2;
				if(maxRepWi < nRepWi1 + nRepWi2)
					maxRepWi = nRepWi1 + nRepWi2;
			}
			
			// show me eight, or ten, intels
			say("maxREPUS1 = " + maxREPUS1);
			say("maxSenWi1 = " + maxSenWi1);
			say("maxRepWi1 = " + maxRepWi1);
			say("maxMinor1 = " + maxMinor1);
			//
			say("maxREPUS2 = " + maxREPUS2);
			say("maxSenWi2 = " + maxSenWi2);
			say("maxRepWi2 = " + maxRepWi2);
			say("maxMinor2 = " + maxMinor2);
			//
			// I just have to prove that Wi1+W2 == 1: Good.
			say("maxSenWi = " + maxSenWi);
			say("maxRepWi = " + maxRepWi);
			
			
			// Next, I shall plot it all myself in pixel-fine detail:

			// I stupidly thought to plot around a vertical centerline.
			// Now I need to rename all X and Y names, need euphemisms:
			// Abscissa is my new horizontal axis, plots republicanism.
			// Ordinate is my new vertical axis, plots fraction [0..1].
			
			int dpi = 300;
			int nBorder = dpi / 10;
			
			// 10000 x 4011 produced 4.5 MB. (10.5 MB if doPlotSubracesWithinWards)

			int nAbscissaPixels = 10000; // a very wide plot to see each ward well
			int nOrdinatePixels = 4011; // decimal ends ..11 for eleven 10% graticules
			
			int imageSizeX = nBorder + nAbscissaPixels + nBorder;
			int imageSizeY = nBorder + nOrdinatePixels + nBorder;
			
			Bitmap bmp = new Bitmap(imageSizeX, imageSizeY);
			bmp.SetResolution(dpi, dpi);
			Graphics gBmp = Graphics.FromImage(bmp);
			
			Brush whiteBrush = new SolidBrush(Color.White);
			gBmp.FillRectangle(whiteBrush, 0, 0, imageSizeX, imageSizeY); // x,y,w,h

			
			// There will be three lines plotted across graph:
			
			float niceLineWidth = 9.0f; // tbd

			// Googled the parties' brand colors:
			Pen redPOTUSPen = new Pen(Color.FromArgb(233, 20, 29), niceLineWidth); // google says RGB: (233, 20, 29)
			Pen bluePOTUSPen = new Pen(Color.FromArgb(0, 21, 188), niceLineWidth); // google says RGB: (0, 21, 188)
			Pen greenVoToPen = new Pen(Color.Green, niceLineWidth);

			
			// Prefer web-safe  colors from [0..255] by += 51:

			// Bluish:
			Brush DemREPUSBrush = new SolidBrush(Color.FromArgb(88, 0,51,255));
			Brush DemSenWiBrush = new SolidBrush(Color.FromArgb(88, 0,102,255));
			Brush DemRepWiBrush = new SolidBrush(Color.FromArgb(88, 0,153,255));
			Brush DemMinorBrush = new SolidBrush(Color.FromArgb(88, 0,204,255));

			// Redish:
			Brush RepREPUSBrush = new SolidBrush(Color.FromArgb(88, 255,51,0));
			Brush RepSenWiBrush = new SolidBrush(Color.FromArgb(88, 255,102,0));
			Brush RepRepWiBrush = new SolidBrush(Color.FromArgb(88, 255,153,0));
			Brush RepMinorBrush = new SolidBrush(Color.FromArgb(88, 255,204,0));
			
			// Oh, unless I just plot one summary metric bar per ward,
			// in which case choose a pale (alpha) color of the party:
			
			Brush RepAverageBrush = new SolidBrush(Color.FromArgb(31, 233, 20, 29));
			Brush DemAverageBrush = new SolidBrush(Color.FromArgb(31, 0, 21, 188));
			
			
			// one-pixel thin graticule lines because they might surround 478 wards:
			Pen black1PixelPen = new Pen(Color.Black, 1.0f);
			// Ehh, maybe two, being drawn first, but keep the math as if one pixel:
			Pen black3PixelPen = new Pen(Color.Black, 3.0f); // say what you mean

			// Draw the eleven 10% horizontal graticule lines:
			for(int y = 1; y <= nOrdinatePixels; y += nOrdinatePixels/10)
			{
				gBmp.DrawLine(black3PixelPen, nBorder, nBorder + y, nBorder + nAbscissaPixels, nBorder + y); // x1, y1, x2, y2
			}

			
			// Draw proportional ward-size bar vertical graticule lines.
			// In fact, this loop turned into the entire plotting loop:

			
			// But first, I need to set ward ordering,
			// and do the ward bar pixel computation:

			
			// List<string> sorts randoms safer than Dict:
			List<string> plotControl = new List<string>();
			
			StringBuilder pcItem = new StringBuilder();
			
			int sumFatness = 0;// for the proportioning
			for(int i = minWardNo; i <= maxWardNo; i++)
			{
				Ward thisWard = wards[i];
				
				// this IF must match generative rule of loop above:
				if(thisWard.TotalBallotsCastInWard < 40)
					continue;
				
				pcItem.Clear();
				
				// Here is the active choice of the best sorting metric:
				pcItem.Append(thisWard.logitAverage.ToString().PadLeft(7));
				
				// I'll need the ward number to re-index data during plot:
				pcItem.Append(thisWard.WardNumber.ToString().PadLeft(4));
				
				// Here is the active choice of ward fatness to plot:
				int fatness = thisWard.TotalBallotsCastInWard;
				pcItem.Append(fatness.ToString().PadLeft(4));
				
				sumFatness += fatness;
				plotControl.Add(pcItem.ToString());
			}

			// double check
			if(nWardsActive != plotControl.Count)
				throw(new Exception("nWardsLeft != plotControl.Count"));

			
			// process from small to large to fix last roundoff in a big ward:

			plotControl.Sort();

			// This list passes ward numbers between loops, saves reparsing an int.
			List<int> plotOrder = new List<int>();
			
			const int leastWardPixels = 4; // at least 1 each for REPUS, SenWi, RepWi, Minor
			int nPreallocated = nWardsActive + 1 + leastWardPixels * nWardsActive;
			int sumHowFat = 0; // should advance from 0 to sumFatness...
			int pixelsRemain = nAbscissaPixels - nPreallocated; // while this decreases

			foreach(String pc in plotControl)
			{
				int wardNo = int.Parse(pc.Substring(7, 4));
				plotOrder.Add(wardNo);
				
				int howFat = int.Parse(pc.Substring(11, 4));
				int unallocated = sumFatness - sumHowFat;
				// The final loop should compute n * m / m => n exactly
				int nAllocate = pixelsRemain * howFat / unallocated;
				sumHowFat += howFat;
				pixelsRemain -= nAllocate;
				wards[wardNo].wardBarPixels = leastWardPixels + nAllocate;
			}
			

			// remember, this plot loop will draw each ward, from LEFT to RIGHT:
			
			// the loop advancement
			int reachedAbscissa = nBorder;
			
			
			// draw the leftmost boundary line even if the loop doDraw bool is false:
			gBmp.DrawLine(black1PixelPen, reachedAbscissa, nBorder, reachedAbscissa, nBorder + nOrdinatePixels); // x1, y1, x2, y2

			
			// Continuously connect the three plotted lines across ward segments:
			int priorDem = 0;
			int priorRep = 0;
			int priorVoTo = 0;
			int priorAbscissa = 0;

			
			foreach(int wardNo in plotOrder)
			{
				Ward thisWard = wards[wardNo];
				
				// remember, this loop runs ABSCISSA from LEFT to RIGHT:
				
				// Optionally draw the graticule line on LEFT SIDE of this ward's BAR:
				if(doDrawGraticulesBetweenEachWard)
					gBmp.DrawLine(black1PixelPen, reachedAbscissa, nBorder, reachedAbscissa, nBorder + nOrdinatePixels); // x1, y1, x2, y2

				// Fill in the BAR GRAPH(S) of this ward:

				// This was actually TMI, too much info:
				if(doPlotSubracesWithinWards)
				{
					// integer coordinates correspond to pixel centers.
					
					// this bar can use up thisWard.wardBarPixels
					// I desire to plot about 4 items in the ward:
					// REPUS, SenWi, RepWi, Minor
					
					int positiveHeight = 0; // variable per each bar item
					int stepAbscissa = reachedAbscissa; // now sitting on lt-side graticule
					int nPixelsRemaining = thisWard.wardBarPixels;

					stepAbscissa ++; // get off the graticule line

					int nMinor = nPixelsRemaining / 4; // often only 1
					nPixelsRemaining -= nMinor;

					// Plot the Democrat positiveHeight Values descending from the roof:
					positiveHeight = (int)((long)nOrdinatePixels * thisWard.ppmDemMinor / 1000000);
					gBmp.FillRectangle(DemMinorBrush, stepAbscissa, nBorder + 1, nMinor, positiveHeight); // x,y,w,h
					// Plot the Republican positiveHeight Values ascending from the floor:
					positiveHeight = (int)((long)nOrdinatePixels * thisWard.ppmRepMinor / 1000000);
					gBmp.FillRectangle(RepMinorBrush, stepAbscissa, nBorder + nOrdinatePixels - positiveHeight, nMinor, positiveHeight); // x,y,w,h

					stepAbscissa += nMinor; // advance past this sub-graph

					int nRepWi = nPixelsRemaining / 3;
					nPixelsRemaining -= nRepWi;

					// Plot the Democrat positiveHeight Values descending from the roof:
					positiveHeight = (int)((long)nOrdinatePixels * thisWard.ppmDemRepWi / 1000000);
					gBmp.FillRectangle(DemRepWiBrush, stepAbscissa, nBorder + 1, nRepWi, positiveHeight); // x,y,w,h
					// Plot the Republican positiveHeight Values ascending from the floor:
					positiveHeight = (int)((long)nOrdinatePixels * thisWard.ppmRepRepWi / 1000000);
					gBmp.FillRectangle(RepRepWiBrush, stepAbscissa, nBorder + nOrdinatePixels - positiveHeight, nRepWi, positiveHeight); // x,y,w,h

					stepAbscissa += nRepWi; // advance past this sub-graph
					
					int nSenWi = nPixelsRemaining / 2;
					nPixelsRemaining -= nSenWi;

					// Plot the Democrat positiveHeight Values descending from the roof:
					positiveHeight = (int)((long)nOrdinatePixels * thisWard.ppmDemSenWi / 1000000);
					gBmp.FillRectangle(DemSenWiBrush, stepAbscissa, nBorder + 1, nSenWi, positiveHeight); // x,y,w,h
					// Plot the Republican positiveHeight Values ascending from the floor:
					positiveHeight = (int)((long)nOrdinatePixels * thisWard.ppmRepSenWi / 1000000);
					gBmp.FillRectangle(RepSenWiBrush, stepAbscissa, nBorder + nOrdinatePixels - positiveHeight, nSenWi, positiveHeight); // x,y,w,h

					stepAbscissa += nSenWi; // advance past this sub-graph
					
					int nREPUS = nPixelsRemaining; // maybe a few

					// Plot the Democrat positiveHeight Values descending from the roof:
					positiveHeight = (int)((long)nOrdinatePixels * thisWard.ppmDemREPUS / 1000000);
					gBmp.FillRectangle(DemREPUSBrush, stepAbscissa, nBorder + 1, nREPUS, positiveHeight); // x,y,w,h
					// Plot the Republican positiveHeight Values ascending from the floor:
					positiveHeight = (int)((long)nOrdinatePixels * thisWard.ppmRepREPUS / 1000000);
					gBmp.FillRectangle(RepREPUSBrush, stepAbscissa, nBorder + nOrdinatePixels - positiveHeight, nREPUS, positiveHeight); // x,y,w,h

				}
				else
				{
					// It seems cleaner to just plot a single pair of Averages per ward:
					
					int positiveHeight = 0;

					// Plot the Democrat positiveHeight Values descending from the roof:
					positiveHeight = (int)((long)nOrdinatePixels * thisWard.ppmDemAverage / 1000000);
					gBmp.FillRectangle(DemAverageBrush, reachedAbscissa + 1, nBorder + 1, thisWard.wardBarPixels, positiveHeight); // x,y,w,h
					// Plot the Republican positiveHeight Values ascending from the floor:
					positiveHeight = (int)((long)nOrdinatePixels * thisWard.ppmRepAverage / 1000000);
					gBmp.FillRectangle(RepAverageBrush, reachedAbscissa + 1, nBorder + nOrdinatePixels - positiveHeight, thisWard.wardBarPixels, positiveHeight); // x,y,w,h
				}


				// Next, while still doing one ward across plot,
				// Plot two HORIZONTAL lines for the POTUS race:
				// Also plot ppmVoterTurnout, a clue to cheating!
				{
					// remember these?
					//int priorDem = 0;
					//int priorRep = 0;
					//int priorVoTo = 0;
					//int priorAbscissa = 0;

					// the left and right graticules are, or will be, at:
					// reachedAbscissa
					// reachedAbscissa + 1 + thisWard.wardBarPixels
					
					// the bar graph was filled from to inclusive:
					// reachedAbscissa + 1
					// reachedAbscissa + thisWard.wardBarPixels
					
					// Let's slightly slope the connecting lines:
					int slight = 1 + thisWard.wardBarPixels / 10;
					int minAbscissa = reachedAbscissa + slight;
					int maxAbscissa = reachedAbscissa + 1 + thisWard.wardBarPixels - slight;
					
					int Dem = nBorder + (int)(thisWard.ppmDemPOTUS * (long)nOrdinatePixels / 1000000);
					int Rep = nBorder + nOrdinatePixels - (int)(thisWard.ppmRepPOTUS * (long)nOrdinatePixels / 1000000);
					int VoTo = nBorder + nOrdinatePixels - (int)(thisWard.ppmVoterTurnout * (long)nOrdinatePixels / 1000000);
					
					if(priorDem != 0)
						gBmp.DrawLine(bluePOTUSPen, priorAbscissa, priorDem, minAbscissa, Dem); // x1, y1, x2, y2
					gBmp.DrawLine(bluePOTUSPen, minAbscissa, Dem, maxAbscissa, Dem); // x1, y1, x2, y2
					
					if(priorRep != 0)
						gBmp.DrawLine(redPOTUSPen, priorAbscissa, priorRep, minAbscissa, Rep); // x1, y1, x2, y2
					gBmp.DrawLine(redPOTUSPen, minAbscissa, Rep, maxAbscissa, Rep); // x1, y1, x2, y2
					
					if(priorVoTo != 0)
						gBmp.DrawLine(greenVoToPen, priorAbscissa, priorVoTo, minAbscissa, VoTo); // x1, y1, x2, y2
					gBmp.DrawLine(greenVoToPen, minAbscissa, VoTo, maxAbscissa, VoTo); // x1, y1, x2, y2
					
					priorDem = Dem;
					priorRep = Rep;
					priorVoTo = VoTo;
					priorAbscissa = maxAbscissa;
				}

				// advance this HUGE loop to the next WARD BAR

				reachedAbscissa += (1 + thisWard.wardBarPixels);
			}

			
			// draw the rightmost boundary line even if the loop doDraw bool is false:
			gBmp.DrawLine(black1PixelPen, reachedAbscissa, nBorder, reachedAbscissa, nBorder + nOrdinatePixels); // x1, y1, x2, y2

			
			// Finally, Make some captions right over the bar graphs
			// don't write right on top of ( i * nOrdinatePixels / 10) = graticules.
			
			Font bigFont = new Font("Arial", 80);
			int bigFontHeight = (int)bigFont.GetHeight(gBmp);
			Brush opaqueBlackTextBrush = new SolidBrush(Color.Black);

			int yHeadine = nBorder + 25 * nOrdinatePixels / 100 - bigFontHeight / 2;
			string Headine = "VISIBLE PROOF OF POTUS 2020 ELECTION FRAUD";
			gBmp.DrawString(Headine, bigFont, opaqueBlackTextBrush, (imageSizeX-gBmp.MeasureString(Headine, bigFont).Width) / 2, yHeadine);
			
			
			Font smallFont = new Font("Arial", 40);
			int smallFontHeight = (int)smallFont.GetHeight(gBmp);
			Brush faintBlackTextBrush = new SolidBrush(Color.FromArgb(127, 0, 0, 0)); // ALPHA 1/4 Black

			int yOneLabel = nBorder + 33 * nOrdinatePixels / 100 - smallFontHeight / 2;
			string OneLabel = "Milwaukee County Wisconsin 2020 Election Wards, ordered by Republicanism";
			gBmp.DrawString(OneLabel, smallFont, faintBlackTextBrush, (imageSizeX-gBmp.MeasureString(OneLabel, smallFont).Width) / 2, yOneLabel);

			int yTwoLabel = nBorder + 37 * nOrdinatePixels / 100 - smallFontHeight / 2;
			string TwoLabel = "Republicanism = Ratio of averages of ward's contested non-POTUS races.";
			gBmp.DrawString(TwoLabel, smallFont, faintBlackTextBrush, (imageSizeX-gBmp.MeasureString(TwoLabel, smallFont).Width) / 2, yTwoLabel);

			int YHuhLabel = nBorder + 43 * nOrdinatePixels / 100 - smallFontHeight / 2;
			string HuhLabel = "Area is non-POTUS average vote; Line is POTUS vote; Blue data is plotted from top down.";
			gBmp.DrawString(HuhLabel, smallFont, faintBlackTextBrush, (imageSizeX-gBmp.MeasureString(HuhLabel, smallFont).Width) / 2, YHuhLabel);

			int yFyiLabel = nBorder + 47 * nOrdinatePixels / 100 - smallFontHeight / 2;
			string FyiLabel = "FRAULD: AREAS AND LINES DO NOT AGREE. Green line is Voter Turnout, fraud clue too.";
			gBmp.DrawString(FyiLabel, smallFont, faintBlackTextBrush, (imageSizeX-gBmp.MeasureString(FyiLabel, smallFont).Width) / 2, yFyiLabel);

			int yHubLabel = nBorder + 95 * nOrdinatePixels / 100 - smallFontHeight / 2;
			string HubLabel = "Source code and input and csv data available at github.com/IneffablePerformativity";
			gBmp.DrawString(HubLabel, smallFont, faintBlackTextBrush, (imageSizeX-gBmp.MeasureString(HubLabel, smallFont).Width) / 2, yHubLabel);

			
			// The work is done. Enjoy the result!
			
			if(File.Exists(pngFilePath))
				File.Delete(pngFilePath);
			
			bmp.Save(pngFilePath, ImageFormat.Png);

		}
	}
}