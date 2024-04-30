using AnkiHistoryVisualization;

var positions_file = @"Data\PeriodicTable.csv";
var database_file = @"Data\collection.anki2";
var deck_name = "Periodic Table";
var card_type = 0;

var positions = CsvUtils.Load<Position>(positions_file);
var data = DataRetriever.Retrieve(database_file, deck_name, card_type);
var images_enumerable = PeriodicTableImagesGenerator.Generate(positions, data);

VideoRenderer.ToVideo(@"PeriodicTable.mp4", 25, images_enumerable);
