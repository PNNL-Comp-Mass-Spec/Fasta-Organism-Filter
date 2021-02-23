namespace FastaOrganismFilter
{
    internal class OrganismInfo
    {
        /// <summary>
        /// Organism name
        /// </summary>
        public string OrganismName { get; }

        /// <summary>
        /// Taxonomy ID
        /// </summary>
        public int TaxonomyID { get; set; }

        /// <summary>
        /// Observation Count
        /// </summary>
        public int ObservationCount { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="organismName"></param>
        /// <param name="taxonomyId"></param>
        public OrganismInfo(string organismName, int taxonomyId = 0)
        {
            OrganismName = organismName;
            TaxonomyID = taxonomyId;
        }
    }
}
