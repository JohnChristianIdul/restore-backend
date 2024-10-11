namespace ReStore___backend.Dtos
{
    public class DemandPredictionDTO
    {
        public class DemandPrediction
        {
            public string Month { get; set; }
            public int ProductID { get; set; }
            public string Product { get; set; }
            public int PredictedDemand { get; set; }
        }

    }
}
