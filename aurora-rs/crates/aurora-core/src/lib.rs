pub mod calc;
pub mod models;
pub mod spring;
pub mod tokens;

pub use calc::*;
pub use models::*;
pub use spring::*;
pub use tokens::*;

pub const AURORA_VERSION: &str = env!("CARGO_PKG_VERSION");
pub const DEFAULT_GITHUB_MANIFEST_URL: &str =
    "https://github.com/MuharremDidici/ProjectAurora/releases/latest/download/release-manifest.json";
pub const DEFAULT_GITHUB_API_LATEST_URL: &str =
    "https://api.github.com/repos/MuharremDidici/ProjectAurora/releases/latest";
