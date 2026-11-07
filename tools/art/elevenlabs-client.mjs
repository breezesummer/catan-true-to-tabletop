import { loadEnvFile } from "node:process";
import { fileURLToPath } from "node:url";
import { ElevenLabsClient } from "@elevenlabs/elevenlabs-js";

if (!process.env.ELEVENLABS_API_KEY) {
  loadEnvFile(fileURLToPath(new URL("../../.env.local", import.meta.url)));
}

export const elevenlabs = new ElevenLabsClient();
