# usage: python gen_octo.py N
import sys
def main():
    if len(sys.argv) < 1:
        print("usage: gen_octo.py N")
        sys.exit(1)
    n = int(sys.argv[1])
    base = 5001
    out = "docker-compose.instances.yml"
    lines = ["services:"]
    for i in range(1, n + 1):
    base = 10000
    out = "docker-compose.instances.yml"
    lines = ["services:"]
    for i in range(0, n):
        volumesText += [
            f"volumes:",
            f"  octo_data_{i}"
        ] 

        name = f"   octoprint_{i}"
        lines += [
            f"{name}:",
            f"      container_name: {name}",
            f"      image: octoprint/octoprint:latest",
            f"      ports: [\"{base+i}:{80}\"]",
            f"      environment:",
            f"          OCTOPRINT_HANDLE_{i}:",
            f"          OCTOPRINT_API_KEY_{i}:",
            f"          OCTOPRINT_BASE_URL_{i}:",
            f"      volumes:",
            f"          - octo_data_{i}:/octoprint",
            f"      networks:",
            f"          - file-processor-network",
            ""
        ]
    with open(out, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
        f.write("\n\n".join(volumesText))
if __name__ == "__main__":
    main()
