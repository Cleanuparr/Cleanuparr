_Love this project? Give it a ⭐️ and let others know!_

# <img width="24px" src="./Logo/256.png" alt="cleanuperr"></img> Cleanuperr

[![Discord](https://img.shields.io/discord/1306721212587573389?color=7289DA&label=Discord&style=for-the-badge&logo=discord)](https://discord.gg/sWggpnmGNY)

Cleanuperr is a tool for automating the cleanup of unwanted or blocked files in Sonarr, Radarr, and supported download clients like qBittorrent. It removes incomplete or blocked downloads, updates queues, and enforces blacklists or whitelists to manage file selection. After removing blocked content, Cleanuperr can also trigger a search to replace the deleted shows/movies.

Cleanuperr was created primarily to address malicious files, such as `*.lnk` or `*.zipx`, that were getting stuck in Sonarr/Radarr and required manual intervention. Some of the reddit posts that made Cleanuperr come to life can be found [here](https://www.reddit.com/r/sonarr/comments/1gqnx16/psa_sonarr_downloaded_a_virus/), [here](https://www.reddit.com/r/sonarr/comments/1gqwklr/sonar_downloaded_a_mkv_file_which_looked_like_a/), [here](https://www.reddit.com/r/sonarr/comments/1gpw2wa/downloaded_waiting_to_import/) and [here](https://www.reddit.com/r/sonarr/comments/1gpi344/downloads_not_importing_no_files_found/).

> [!IMPORTANT]
> **Features:**
> - Strike system to mark stalled or downloads stuck in metadata downloading.
> - Remove and block downloads that reached a maximum number of strikes.
> - Remove and block downloads that have a low download speed or high estimated completion time.
> - Remove downloads blocked by qBittorrent or by Cleanuperr's **content blocker**.
> - Trigger a search for downloads removed from the *arrs.
> - Remove downloads that have been seeding for a certain amount of time.
> - Remove downloads that have no hardlinks (have been upgraded by the *arrs).
> - Notify on strike or download removal.
> - Ignore certain torrent hashes, categories, tags or trackers from being processed by Cleanuperr.

Cleanuperr supports both qBittorrent's built-in exclusion features and its own blocklist-based system. Binaries for all platforms are provided, along with Docker images for easy deployment.

# Docs

Docs can be found [here](https://flmorg.github.io/cleanuperr/).

# Credits
Special thanks for inspiration go to:
- [ThijmenGThN/swaparr](https://github.com/ThijmenGThN/swaparr)
- [ManiMatter/decluttarr](https://github.com/ManiMatter/decluttarr)
- [PaeyMoopy/sonarr-radarr-queue-cleaner](https://github.com/PaeyMoopy/sonarr-radarr-queue-cleaner)
- [Sonarr](https://github.com/Sonarr/Sonarr) & [Radarr](https://github.com/Radarr/Radarr)

# Buy me a coffee
If I made your life just a tiny bit easier, consider buying me a coffee!

<a href="https://buymeacoffee.com/flaminel" target="_blank"><img src="https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png" alt="Buy Me A Coffee" style="height: 41px !important;width: 174px !important;box-shadow: 0px 3px 2px 0px rgba(190, 190, 190, 0.5) !important;-webkit-box-shadow: 0px 3px 2px 0px rgba(190, 190, 190, 0.5) !important;" ></a>
