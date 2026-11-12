import type { createApiClient } from './client.ts';
import { createUuid } from '../utils/uuid.ts';
export type Machine={id:string;assetCode:string;name:string;mode:string};
export type DeviceSession={sessionId:string;deviceId:string;profileId:string;domainId:string;deviceName:string};
export type ProfileFile={fileId:string;name:string;folder:string;sizeBytes:number;extension:string};
export type MachineProfile=DeviceSession & {files:ProfileFile[];quota:{maxFiles:number;usedFiles:number;reservedFiles:number;remainingFiles:number;overQuota:boolean};contentBase64?:string|null};
export type FileOperation={kind:string;requestId?:string;folder?:string;name?:string;newName?:string;targetFolder?:string;fileId?:string;contentBase64?:string};
export function createMachineApi(api: ReturnType<typeof createApiClient>) { return { profileRequest(device:string,session:string,signal?:AbortSignal){return api<MachineProfile>(`/machines/${encodeURIComponent(device)}/profile`,{headers:{'X-Device-Session':session},signal})}
,operate(device:string,session:string,operation:FileOperation,signal?:AbortSignal){return api<MachineProfile>(`/machines/${encodeURIComponent(device)}/files/operations`,{method:'POST',headers:{'X-Device-Session':session},body:JSON.stringify({...operation,requestId:operation.requestId??createUuid()}),signal})}
}; }
export function fileAllowed(name:string){return /\.(docx?|xlsx?|pptx?|pdf|txt|csv|ini)$/i.test(name)}
export function encodeBytes(bytes:Uint8Array){let s='';for(const b of bytes)s+=String.fromCharCode(b);return btoa(s)}
