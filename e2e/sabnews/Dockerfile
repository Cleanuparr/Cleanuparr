# Fake NNTP news server used by the e2e SABnzbd tests.
#
# tests/sabnews.py lives in the SABnzbd repo itself, not here, so this image
# fetches it from a pinned commit at build time instead of vendoring a copy.
# Bump SABNZBD_COMMIT to pick up an upstream change.
FROM python:3.12-slim

ARG SABNZBD_COMMIT=a647f73426cb70ad60ae7964b96c2d5e939b4727

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && pip install --no-cache-dir sabctools \
    && curl -fsSL "https://raw.githubusercontent.com/sabnzbd/sabnzbd/${SABNZBD_COMMIT}/tests/sabnews.py" -o /sabnews.py \
    && apt-get purge -y curl

WORKDIR /data
EXPOSE 8888

ENTRYPOINT ["python3", "/sabnews.py"]
CMD ["-s", "0.0.0.0", "-p", "8888"]
