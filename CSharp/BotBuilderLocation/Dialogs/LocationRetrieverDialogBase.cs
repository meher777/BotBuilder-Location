﻿namespace Microsoft.Bot.Builder.Location.Dialogs
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Bing;
    using Builder.Dialogs;
    using Internals.Fibers;

    [Serializable]
    abstract class LocationRetrieverDialogBase : LocationDialogBase<LocationDialogResponse>
    {
        protected readonly string apiKey;
        protected readonly IGeoSpatialService geoSpatialService;

        private readonly LocationOptions options;
        private readonly LocationRequiredFields requiredFields;
        private Location selectedLocation;

        internal LocationRetrieverDialogBase(
            string apiKey,
            IGeoSpatialService geoSpatialService,
            LocationOptions options,
            LocationRequiredFields requiredFields,
            LocationResourceManager resourceManager) : base(resourceManager)
        {
            SetField.NotNull(out this.apiKey, nameof(apiKey), apiKey);
            SetField.NotNull(out this.geoSpatialService, nameof(geoSpatialService), geoSpatialService);
            this.options = options;
            this.requiredFields = requiredFields;
        }

        protected async Task ProcessRetrievedLocation(IDialogContext context, Location retrievedLocation)
        {
            this.selectedLocation = retrievedLocation;
            await this.TryReverseGeocodeAddress(this.selectedLocation);

            if (this.requiredFields != LocationRequiredFields.None)
            {
                var requiredDialog = new LocationRequiredFieldsDialog(this.selectedLocation, this.requiredFields, this.ResourceManager);
                context.Call(requiredDialog, this.ResumeAfterChildDialogAsync);
            }
            else
            {
                context.Done(new LocationDialogResponse(this.selectedLocation));
            }
        }

        /// <summary>
        /// Resumes after native location dialog returns.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="result">The result.</param>
        /// <returns>The asynchronous task.</returns>
        internal override async Task ResumeAfterChildDialogInternalAsync(IDialogContext context, IAwaitable<LocationDialogResponse> result)
        {
            // required fields have potentially changed after the child required fields dialog
            // hence, re-assign the value of the selected location
            this.selectedLocation = (await result).Location;
            context.Done(new LocationDialogResponse(this.selectedLocation));
        }

        /// <summary>
        /// Tries to complete missing fields using Bing reverse geo-coder.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <returns>The asynchronous task.</returns>
        private async Task TryReverseGeocodeAddress(Location location)
        {
            // If user passed ReverseGeocode flag and dialog returned a geo point,
            // then try to reverse geocode it using BingGeoSpatialService.
            if (this.options.HasFlag(LocationOptions.ReverseGeocode) && location != null && location.Address == null && location.Point != null)
            {
                var results = await this.geoSpatialService.GetLocationsByPointAsync(this.apiKey, location.Point.Coordinates[0], location.Point.Coordinates[1]);
                var geocodedLocation = results?.Locations?.FirstOrDefault();
                if (geocodedLocation?.Address != null)
                {
                    // We don't trust reverse geo-coder on the street address level,
                    // so copy all fields except it.
                    // TODO: do we need to check the returned confidence level?
                    location.Address = new Bing.Address
                    {
                        CountryRegion = geocodedLocation.Address.CountryRegion,
                        AdminDistrict = geocodedLocation.Address.AdminDistrict,
                        AdminDistrict2 = geocodedLocation.Address.AdminDistrict2,
                        Locality = geocodedLocation.Address.Locality,
                        PostalCode = geocodedLocation.Address.PostalCode
                    };
                }
            }
        }
    }
}
