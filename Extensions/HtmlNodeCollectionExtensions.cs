﻿using CsvHelper;
using HtmlAgilityPack;
using prospect_scraper_mddb_2022.DTOs;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;

namespace prospect_scraper_mddb_2022.Extensions
{
    public static class HtmlNodeCollectionExtensions
    {
        public static List<ProspectRanking> FindProspects(this HtmlNodeCollection nodes, string todayString)
        {
            var prospectRankings = new List<ProspectRanking>();

            //read in CSV from info/RanksToProjectedPoints.csv
            var dt = new DataTable();
            using (var reader = new StreamReader(Path.Combine("info", "RanksToProjectedPoints.csv")))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                // Do any configuration to `CsvReader` before creating CsvDataReader.
                using var dr = new CsvDataReader(csv);
                dt.Load(dr);
            }
            // Transform datatable dt to dictionary
            var ranksToPoints = dt.AsEnumerable()
                    .ToDictionary<DataRow, string, string>(row => row.Field<string>(0),
                                                        row => row.Field<string>(1));

            var dt2 = new DataTable();
            using (var reader = new StreamReader(Path.Combine("info", "SchoolStatesAndConferences.csv")))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                // Do any configuration to `CsvReader` before creating CsvDataReader.
                using var dr = new CsvDataReader(csv);
                dt2.Load(dr);
            }

            var schoolsToStatesAndConfs = dt2.AsEnumerable()
                    .ToDictionary(row => row.Field<string>(0),
                                  row => (row.Field<string>(1), row.Field<string>(2))
                                 );

            foreach (var node in nodes)
            {
                var pickContainer = node.Descendants().FirstOrDefault(n => n.HasClass("pick-container"));
                var playerContainer = node.Descendants().FirstOrDefault(n => n.HasClass("player-container"));
                var percentageContainer = node.Descendants().FirstOrDefault(n => n.HasClass("percentage-container"));
                string projectedDraftSpot = "";
                string projectedDraftTeam = "";
                string playerSchool = "";

                var actualPickStuff = pickContainer.FirstChild;
                string currentRank = actualPickStuff.FirstChild.InnerText;
                var peakRankHtml = actualPickStuff.LastChild; //Rank 1 is in the middle child, not the last child for some reason. Seems to l=only happen when actualPickStuff.LastChild has 3 children.
                string peakRank = peakRankHtml.ChildNodes[1].InnerText;  // this is inside a span, but I'm not sure if it's reliably the second element.
                var namePositionSchool = node.LastChild;
                string playerName = playerContainer.FirstChild.InnerText.Replace("&#39;", "'");
                string playerPosition = playerContainer.LastChild.FirstChild.InnerText.Replace("|", "").Trim();
                int afterPipeStringLength = playerContainer.LastChild.FirstChild.InnerText.Split("|")[1].Length;
                if (playerContainer.LastChild.ChildNodes.Count == 2 && afterPipeStringLength <= 2)
                {
                    playerSchool = playerContainer.LastChild.LastChild.InnerText.Replace("&amp;", "&");
                }
                else if (afterPipeStringLength > 2)
                {
                    playerSchool = playerContainer.LastChild.FirstChild.InnerText.Split("|")[1].Replace("&amp;", "&").Trim();
                }
                else
                {
                    playerSchool = playerSchool = playerContainer.LastChild.ChildNodes[1].InnerText.Replace("&amp;", "&");
                }
                if (percentageContainer != null)
                {
                    int percentageContainerChildNodeCount = percentageContainer.ChildNodes.Count;
                    if (percentageContainerChildNodeCount == 2)
                    {
                        //if projected draft spot starts with "Possible" then it's a general grade with no consensus.
                        projectedDraftSpot = percentageContainer.FirstChild.LastChild.InnerText.Replace("#", "").Replace(":", "");
                        projectedDraftTeam = percentageContainer.LastChild.InnerText;
                        if (projectedDraftTeam != "No Consensus Available")
                        {
                            string projectedDraftTeamHref = percentageContainer.LastChild.FirstChild.Attributes.FirstOrDefault()?.Value;
                            string[] hrefStrings = projectedDraftTeamHref?.Split("/");
                            projectedDraftTeam = hrefStrings[^1].Replace("-", " ").ToUpper();
                        }
                    }
                }

                //var ranking = new ProspectRanking();

                playerSchool = playerSchool.ConvertSchool();
                string leagifyPoints = ranksToPoints.GetValueOrDefault(currentRank, "1");

                (string schoolConference, string schoolState) = schoolsToStatesAndConfs.GetValueOrDefault(playerSchool, ("", ""));

                Console.WriteLine($"Player: {playerName} at rank {currentRank} from {playerSchool} playing {playerPosition} got up to peak rank {peakRank} with {leagifyPoints} possible points");

                var currentPlayer = new ProspectRanking(todayString, currentRank, peakRank, playerName, playerSchool, playerPosition, schoolState, schoolConference, leagifyPoints, projectedDraftSpot, projectedDraftTeam);
                prospectRankings.Add(currentPlayer);
            }

            return prospectRankings;
        }
    }
}