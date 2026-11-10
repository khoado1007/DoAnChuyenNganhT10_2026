import { api } from './auth';
import { createFileReadApi } from './file-read';
import { createFileUploadApi } from './file-upload';

export const filesApi = { ...createFileReadApi(api), ...createFileUploadApi(api) };
