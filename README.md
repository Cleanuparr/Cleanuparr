_Love this project? Give it a ⭐️ and let others know!_

# <img width="24px" src="./Logo/256.png" alt="Cleanuparr"></img> Cleanuparr

[![Discord](https://img.shields.io/discord/1306721212587573389?color=7289DA&label=Discord&style=for-the-badge&logo=discord)](https://discord.gg/SCtMCgtsc4)

Cleanuparr is a tool for automating the cleanup of unwanted or blocked files in Sonarr, Radarr, and supported download clients like qBittorrent. It removes incomplete or blocked downloads, updates queues, and enforces blacklists or whitelists to manage file selection. After removing blocked content, Cleanuparr can also trigger a search to replace the deleted shows/movies.

Cleanuparr was created primarily to address malicious files, such as `*.lnk` or `*.zipx`, that were getting stuck in Sonarr/Radarr and required manual intervention. Some of the reddit posts that made Cleanuparr come to life can be found [here](https://www.reddit.com/r/sonarr/comments/1gqnx16/psa_sonarr_downloaded_a_virus/), [here](https://www.reddit.com/r/sonarr/comments/1gqwklr/sonar_downloaded_a_mkv_file_which_looked_like_a/), [here](https://www.reddit.com/r/sonarr/comments/1gpw2wa/downloaded_waiting_to_import/) and [here](https://www.reddit.com/r/sonarr/comments/1gpi344/downloads_not_importing_no_files_found/).

> [!IMPORTANT]
> **Features:**
> - Strike system to mark bad downloads.
> - Remove and block downloads that reached a maximum number of strikes.
> - Remove and block downloads that are **failing to be imported** by the arrs.
> - Remove and block downloads that are **stalled** or in **metadata downloading** state.
> - Remove and block downloads that have a **low download speed** or **high estimated completion time**.
> - Remove and block downloads blocked by qBittorrent or by Cleanuparr's **Content Blocker**.
> - Automatically trigger a search for downloads removed from the arrs.
> - Clean up downloads that have been **seeding** for a certain amount of time.
> - Remove downloads that are **orphaned**/have no **hardlinks**/are not referenced by the arrs anymore (with [cross-seed](https://www.cross-seed.org/) support).
> - Notify on strike or download removal.
> - Ignore certain torrent hashes, categories, tags or trackers from being processed by Cleanuparr.

## 🎯 Supported Applications

### *Arr Applications
- **Sonarr**
- **Radarr**
- **Lidarr**
- **Readarr**
- **Whisparr**

### Download Clients
- **qBittorrent**
- **Transmission**
- **Deluge**
- **µTorrent**

### Platforms
- **Docker**
- **Windows**
- **macOS**
- **Linux**
- **Unraid**

## 🚀 Quick Start

```bash
docker run -d --name cleanuparr \
  --restart unless-stopped \
  -p 11011:11011 \
  -v /path/to/config:/config \
  -e PORT=11011 \
  -e PUID=1000 \
  -e PGID=1000 \
  -e TZ=Etc/UTC \
  ghcr.io/cleanuparr/cleanuparr:latest
```

For Docker Compose, health checks, and other installation methods, see the [Complete Installation Guide](https://cleanuparr.github.io/Cleanuparr/docs/installation/detailed).

### 🌐 Access the Web Interface

After installation, open your browser and navigate to:
```
http://localhost:11011
```

**Next Steps:** Check out the [📖 Complete Documentation](https://cleanuparr.github.io/Cleanuparr/) for detailed configuration guides and setup instructions.

## 📖 Documentation & Support

- **📚 [Complete Documentation](https://cleanuparr.github.io/Cleanuparr/)** - Installation guides, configuration, and troubleshooting
- **⚙️ [Configuration Guide](https://cleanuparr.github.io/Cleanuparr/docs/category/configuration)** - Set up download clients, *arr apps, and features
- **🔧 [Setup Scenarios](https://cleanuparr.github.io/Cleanuparr/docs/category/setup-scenarios)** - Common use cases and examples
- **💬 [Discord Community](https://discord.gg/SCtMCgtsc4)** - Get help and discuss with other users
- **🔗 [GitHub Releases](https://github.com/Cleanuparr/Cleanuparr/releases)** - Download binaries and view changelog

# <img style="vertical-align: middle;" width="24px" src="./Logo/256.png" alt="Cleanuparr"> <span style="vertical-align: middle;">Cleanuparr</span> <img src="https://raw.githubusercontent.com/FortAwesome/Font-Awesome/6.x/svgs/solid/x.svg" height="24px" width="30px" style="vertical-align: middle;"> <span style="vertical-align: middle;">Huntarr</span> <img style="vertical-align: middle;" width="24px" src="https://github.com/plexguide/Huntarr.io/blob/main/frontend/static/logo/512.png?raw=true" alt Huntarr></img>

Think of **Cleanuparr** as the janitor of your server; it keeps your download queue spotless, removes clutter, and blocks malicious files. Now imagine combining that with **Huntarr**, the compulsive librarian who finds missing and upgradable media to complete your collection

While **Huntarr** fills in the blanks and improves what you already have, **Cleanuparr** makes sure that only clean downloads get through. If you're aiming for a reliable and self-sufficient setup, **Cleanuparr** and **Huntarr** will take your automated media stack to another level.

<span style="font-size:24px"> ➡️ [**Huntarr**](https://github.com/plexguide/Huntarr.io) <span style="vertical-align: middle">![Huntarr](https://img.shields.io/github/stars/plexguide/Huntarr.io?style=social)</span></span> 

# Credits
Special thanks for inspiration go to:
- [ThijmenGThN/swaparr](https://github.com/ThijmenGThN/swaparr)
- [ManiMatter/decluttarr](https://github.com/ManiMatter/decluttarr)
- [PaeyMoopy/sonarr-radarr-queue-cleaner](https://github.com/PaeyMoopy/sonarr-radarr-queue-cleaner)
- [Sonarr](https://github.com/Sonarr/Sonarr) & [Radarr](https://github.com/Radarr/Radarr)

# Buy me a coffee
If I made your life just a tiny bit easier, consider buying me a coffee!

<a href="https://buymeacoffee.com/flaminel" target="_blank"><img src="https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png" alt="Buy Me A Coffee" style="height: 41px !important;width: 174px !important;box-shadow: 0px 3px 2px 0px rgba(190, 190, 190, 0.5) !important;-webkit-box-shadow: 0px 3px 2px 0px rgba(190, 190, 190, 0.5) !important;" ></a>
