using AnkiHistoryVisualization;

var database_file = @"Data\collection.anki2";

{
    var positions = CsvUtils.Load<Position>(@"Data\PeriodicTable.csv");
    var data = DataRetriever.Retrieve(database_file, "Periodic Table", cardType: 0);
    var images_enumerable = new PeriodicTableImagesGenerator(positions).Generate(data);

    VideoRenderer.ToVideo(@"PeriodicTable.mp4", 25, images_enumerable);
}

{
    var data = DataRetriever.Retrieve(database_file, "USA States", cardType: 0);
    var regions = SvgParser.Parse(@"data\Map_USA.svg");
    var images_enumerable = new MapImageGenerator(regions).Generate(data);

    VideoRenderer.ToVideo("USAStates.mp4", 25, images_enumerable);
}
