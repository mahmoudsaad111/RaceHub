import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface ChatMessageDto {
  messageId: string;
  conversationId: string;
  senderId: string;
  content: string;
  sentAtUtc: string;
  isRead: boolean;
}

export interface ConversationDto {
  conversationId: string;
  type: 'friend' | 'race';
  name: string;
  avatar: string | null;
  otherUserId: string;
  otherUserDisplayName: string;
  unreadCount: number;
  lastMessage: ChatMessageDto | null;
}

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly authService = inject(AuthService);

  private connection: signalR.HubConnection | null = null;
  private startPromise: Promise<void> | null = null;

  readonly messages$ = new Subject<ChatMessageDto>();
  readonly newMessageNotification$ = new Subject<{ conversationId: string; message: ChatMessageDto }>();
  readonly chatError$ = new Subject<{ error: string; code?: string }>();

  async ensureConnected(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    if (!this.startPromise) {
      this.startPromise = this.start();
    }

    await this.startPromise;
  }

  async disconnect(): Promise<void> {
    this.startPromise = null;
    await this.connection?.stop();
    this.connection = null;
  }

  async joinConversation(conversationId: string): Promise<void> {
    await this.ensureConnected();
    await this.connection!.invoke('JoinConversation', conversationId);
  }

  async leaveConversation(conversationId: string): Promise<void> {
    await this.connection!.invoke('LeaveConversation', conversationId);
  }

  sendFriendMessage(friendId: string, content: string): Promise<void> {
    return this.invoke('SendFriendMessage', friendId, content);
  }

  getConversationHistory(conversationId: string, skip = 0, take = 50): Promise<ChatMessageDto[]> {
    return this.invoke('GetConversationHistory', conversationId, skip, take);
  }

  private async start(): Promise<void> {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.apiUrl.replace(/\/api\/?$/, '')}/hubs/chat`, {
        accessTokenFactory: () => this.authService.getAccessToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(environment.production ? signalR.LogLevel.Warning : signalR.LogLevel.Information)
      .build();

    connection.on('ReceiveMessage', (dto: ChatMessageDto) => this.messages$.next(dto));
    connection.on('NewMessageNotification', (dto: { conversationId: string; message: ChatMessageDto }) => this.newMessageNotification$.next(dto));
    connection.on('ChatError', (dto: { error: string; code?: string }) => this.chatError$.next(dto));

    this.connection = connection;
    await connection.start();
  }

  private async invoke(method: string, ...args: unknown[]): Promise<any> {
    await this.ensureConnected();
    return this.connection!.invoke(method, ...args);
  }
}
