﻿using System;
using System.Collections.Generic;
using System.Linq;
using JohnsonControls.Metasys.BasicServices;
using Microsoft.Extensions.Logging;

namespace MetasysServicesExampleApp.FeaturesDemo
{
    public class SpacesDemo
    {
        private MetasysClient client;
        private LogInitializer<SpacesDemo> log;

        public SpacesDemo(MetasysClient client)
        {
            this.client = client;
            log = new LogInitializer<SpacesDemo>();
        }
        public void Run()
        {
            try {
                Console.WriteLine("\n\nGetSpaceTypes...");

                IEnumerable<MetasysObjectType> spaceTypes = client.GetSpaceTypes();
                foreach (var type in spaceTypes)
                {
                    Console.WriteLine($"\nType {type.Id}: {type.Description}, ");
                }
                // Select a space type
                Console.WriteLine("\nPlease enter the Space Type ID to retrieve all related spaces:");
                string spaceType = Console.ReadLine();
                IEnumerable<MetasysObject> spaces = client.GetSpaces(spaceType);
                Console.WriteLine($"Spaces found: {spaces.Count()}");
                foreach (var space in spaces)
                {
                    Console.WriteLine($"\n{space.Id}: {space.Name}, {space.ItemReference}");
                }
                // Select a space            
                Console.WriteLine("\nPlease enter the Space ID to retrieve all related equipment:");
                string spaceID = Console.ReadLine();
                IEnumerable<MetasysObject> spaceEquipment = client.GetSpaceEquipment(new Guid(spaceID));
                Console.WriteLine($"Equipment found: {spaceEquipment.Count()}");
                foreach (var o in spaceEquipment)
                {
                    Console.WriteLine($"\n{o.Id}: {o.Name}, {o.ItemReference}");
                }
                // Select a point
                Console.WriteLine("\nPlease enter the equipment ID to retrieve all related points:");
                string equipmentID = Console.ReadLine();
                IEnumerable<Point> equipmentPoints = client.GetEquipmentPoints(new Guid(equipmentID));
                Console.WriteLine($"Equipment found: {equipmentPoints.Count()}");
                foreach (var p in equipmentPoints)
                {
                    Console.WriteLine($"\n{p.ShortName}: {p.Label}, {p.PresentValue?.StringValue}");
                }
            }
            catch (Exception exception) {
                log.logger.LogError(string.Format("An error occured while getting space information - {0}", exception.Message));
                Console.WriteLine("\n \nAn Error occurred. Press Enter to return to Main Menu");
            }
            Console.ReadLine();
        }
    }
}
