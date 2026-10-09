namespace EnjoylixSDK.Config
{
    /// <summary>
    /// How the SDK decides whether a WebGL build is running inside the Enjoylix portal.
    /// Only affects WebGL player builds; the Editor and native platforms are never "portal".
    /// </summary>
    public enum PortalMode
    {
        /// <summary>Portal when the page is embedded in an iframe (<c>window.parent !== window</c>), standalone otherwise.</summary>
        Auto,

        /// <summary>Always treat the build as running inside the Enjoylix portal.</summary>
        Portal,

        /// <summary>Never treat the build as running inside the Enjoylix portal, even when embedded in an iframe.</summary>
        Standalone
    }
}
