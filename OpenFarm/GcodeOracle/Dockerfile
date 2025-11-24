FROM python:3.11.9
LABEL authors="matthewelliott"

WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    libgl1-mesa-glx \
    libegl1-mesa \
    libgles2-mesa \
    libosmesa6 \
    xvfb

COPY GcodeOracle/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY GcodeOracle/main.py .
COPY GcodeOracle/Renderer.py .
COPY GcodeOracle/GcodeManager.py .
COPY GcodeOracle/Entrypoint.sh .

ENV DISPLAY=:99
ENV MESA_GL_VERSION_OVERRIDE=3.3

RUN chmod +x Entrypoint.sh

ENTRYPOINT ["./Entrypoint.sh"]
VOLUME /app